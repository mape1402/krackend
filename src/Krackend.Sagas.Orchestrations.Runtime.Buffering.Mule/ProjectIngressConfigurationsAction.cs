using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Projects ingress configuration from an accepted artifact and marks it ready.
/// </summary>
[MuleAction(RuntimeArtifactActionNames.ProjectIngressConfigurations)]
public sealed class ProjectIngressConfigurationsAction : IMuleAction<RuntimeArtifactProjectionRequest>
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeIngressConfigurationProjector _projector;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IRuntimeArtifactReadyNotifier _artifactReadyNotifier;
    private readonly ILogger<ProjectIngressConfigurationsAction> _logger;
    private readonly IMuleTerminalFailureMarker _terminalFailureMarker;
    private readonly IIngressRegistry _ingressRegistry;
    private readonly IRuntimeIngressConfigurationRepository _ingress;
    private readonly IRuntimeTriggerClaimRepository _claims;
    private readonly IRuntimeArtifactDeploymentHistoryRepository _history;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectIngressConfigurationsAction"/> class.
    /// </summary>
    public ProjectIngressConfigurationsAction(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeIngressConfigurationProjector projector,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactReadyNotifier artifactReadyNotifier,
        ILogger<ProjectIngressConfigurationsAction> logger,
        IMuleTerminalFailureMarker terminalFailureMarker,
        IIngressRegistry ingressRegistry = null,
        IRuntimeIngressConfigurationRepository ingress = null,
        IRuntimeTriggerClaimRepository claims = null,
        IRuntimeArtifactDeploymentHistoryRepository history = null)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _artifactReadyNotifier = artifactReadyNotifier ?? throw new ArgumentNullException(nameof(artifactReadyNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _terminalFailureMarker = terminalFailureMarker ?? throw new ArgumentNullException(nameof(terminalFailureMarker));
        _ingressRegistry = ingressRegistry;
        _ingress = ingress;
        _claims = claims;
        _history = history;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        MuleActionContext<RuntimeArtifactProjectionRequest> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Payload ?? throw new InvalidOperationException("Projection request payload is required.");
        var artifactId = new Id(System.Ulid.Parse(request.ArtifactId));
        var artifact = await _artifactRepository.GetById(artifactId, cancellationToken);
        if (artifact.IngressGeneration != request.IngressGeneration)
        {
            await MarkRollbackFailedAsync(request, cancellationToken);
            if ((request.RollbackTargetTriggerKeys?.Length ?? 0) > 0 &&
                !string.IsNullOrWhiteSpace(request.RollbackOperationId) && artifact.IsActive &&
                artifact.Status == RuntimeOrchestrationArtifactStatus.Ready)
            {
                // A rollback can commit before its notification is sent. Re-reading
                // the current generation makes a durable retry refresh active ingress.
                var refresh = RuntimeArtifactReadyGossipMessage.FromArtifact(artifact);
                refresh.RefreshToken = request.RollbackOperationId;
                await _artifactReadyNotifier.NotifyReadyAsync(refresh, cancellationToken);
                await NotifyRollbackSourcesAsync(request, cancellationToken);
            }
            _logger.LogInformation(
                "Skipping stale ingress projection for artifact {ArtifactId}. Requested generation {RequestedGeneration}, current generation {CurrentGeneration}.",
                request.ArtifactId,
                request.IngressGeneration,
                artifact.IngressGeneration);
            return;
        }

        try
        {
            var rollback = (request.RollbackTargetTriggerKeys?.Length ?? 0) > 0;
            if (rollback && string.IsNullOrWhiteSpace(request.RollbackOperationId))
                throw new IngressProjectionConfigurationException("Rollback operation id is required to refresh runtime replicas.");
            if (!rollback && artifact.Status != RuntimeOrchestrationArtifactStatus.Ready)
            {
                artifact = await ProjectIngressConfigurationsAsync(request, artifact, artifactId, cancellationToken);
            }

            var staged = rollback || (request.RolloutReplacements?.Length ?? 0) > 0;
            if (staged && (rollback || artifact.Status != RuntimeOrchestrationArtifactStatus.Ready))
            {
                if (artifact.IngressGeneration != request.IngressGeneration || !artifact.IsActive ||
                    artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active)
                {
                    await MarkRollbackFailedAsync(request, cancellationToken);
                    return;
                }
                await ActivateRolloutAsync(request, artifactId, cancellationToken);
                artifact = await _artifactRepository.GetById(artifactId, cancellationToken);
            }
            var readyMessage = RuntimeArtifactReadyGossipMessage.FromArtifact(artifact);
            if (rollback) readyMessage.RefreshToken = request.RollbackOperationId;
            await _artifactReadyNotifier.NotifyReadyAsync(readyMessage, cancellationToken);
            if (rollback) await NotifyRollbackSourcesAsync(request, cancellationToken);
        }
        catch (IngressProjectionConfigurationException exception)
        {
            _logger.LogWarning(
                exception,
                "Ingress projection for artifact {ArtifactId} generation {IngressGeneration} failed permanently because the artifact configuration is invalid.",
                request.ArtifactId,
                request.IngressGeneration);

            _terminalFailureMarker.MarkTerminal(context);
            throw;
        }
    }

    private async Task NotifyRollbackSourcesAsync(RuntimeArtifactProjectionRequest request, CancellationToken cancellationToken)
    {
        foreach (var sourceId in (request.RolloutReplacements ?? []).Select(x => x.ArtifactId).Distinct(StringComparer.Ordinal))
        {
            var source = await _artifactRepository.GetById(new Id(System.Ulid.Parse(sourceId)), cancellationToken);
            if (!source.IsActive || source.Status != RuntimeOrchestrationArtifactStatus.Ready) continue;
            var refresh = RuntimeArtifactReadyGossipMessage.FromArtifact(source);
            refresh.RefreshToken = request.RollbackOperationId;
            await _artifactReadyNotifier.NotifyReadyAsync(refresh, cancellationToken);
        }
    }

    private async Task<RuntimeOrchestrationArtifact> ProjectIngressConfigurationsAsync(
        RuntimeArtifactProjectionRequest request,
        RuntimeOrchestrationArtifact artifact,
        Id artifactId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            using (_unitOfWork.DeferAutoSave())
            {
                if ((request.RolloutReplacements?.Length ?? 0) > 0)
                {
                    if (_artifactRepository is not IRuntimeArtifactRolloutTransitionRepository transitions)
                        throw new IngressProjectionConfigurationException("The runtime storage cannot prepare a staged rollout.");
                    if (!await transitions.TryMarkRolloutProjectionStarted(artifactId, request.IngressGeneration, cancellationToken))
                        return await _artifactRepository.GetById(artifactId, cancellationToken);
                }
                else
                    await _artifactRepository.MarkProjectionStarted(artifactId, request.IngressGeneration, cancellationToken);
                await _projector.ProjectAsync(artifact, cancellationToken);
                if ((request.RolloutReplacements?.Length ?? 0) == 0)
                {
                    await _artifactRepository.MarkReady(
                        artifactId,
                        request.IngressGeneration,
                        cancellationToken);
                }
                await _unitOfWork.SaveChanges(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return await _artifactRepository.GetById(artifactId, cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkProjectionFailedAsync(artifactId, request.IngressGeneration, exception,
                (request.RolloutReplacements?.Length ?? 0) > 0, cancellationToken);
            throw;
        }
    }

    private async Task ActivateRolloutAsync(RuntimeArtifactProjectionRequest request, Id artifactId, CancellationToken cancellationToken)
    {
        try
        {
            if ((request.RollbackTargetTriggerKeys?.Length ?? 0) > 0)
            {
                if (_ingressRegistry is not IRollbackIngressRegistry rollback)
                    throw new IngressStandupConfigurationException("The runtime does not support staged rollback ingress.");
                await using var preparation = await rollback.PrepareRollbackAsync(request.ArtifactId, request.IngressGeneration, request.RollbackTargetTriggerKeys, cancellationToken);
                await RuntimeArtifactRolloutActivation.ActivateAsync(request, _artifactRepository, _unitOfWork,
                    _ingress, _claims, cancellationToken, _history);
                preparation.Complete();
                return;
            }
            else
            {
                if (_ingressRegistry is not IStagedIngressRegistry staged)
                    throw new IngressStandupConfigurationException("The runtime does not support staged rollout ingress.");
                await staged.StandUpStagedAsync(request.ArtifactId, request.IngressGeneration, cancellationToken);
            }
            await RuntimeArtifactRolloutActivation.ActivateAsync(request, _artifactRepository, _unitOfWork,
                _ingress, _claims, cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                var current = await _artifactRepository.GetById(artifactId, CancellationToken.None);
                if (current.IngressGeneration == request.IngressGeneration &&
                    current.Status != RuntimeOrchestrationArtifactStatus.Ready && _ingressRegistry is not null)
                    await _ingressRegistry.ShutDownOneAsync(request.ArtifactId, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Could not clean up failed staged ingress for {ArtifactId}.", artifactId);
            }
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            await MarkRollbackFailedAsync(request, cancellationToken);
            try
            {
                if (_artifactRepository is IRuntimeArtifactRolloutTransitionRepository transitions)
                    await transitions.TryMarkRolloutStandupFailed(artifactId, request.IngressGeneration, exception.Message, cancellationToken);
            }
            catch (Exception markException)
            {
                _logger.LogWarning(markException, "Could not record staged rollout standup failure for {ArtifactId}.", artifactId);
            }
            throw;
        }
    }

    private async Task MarkRollbackFailedAsync(RuntimeArtifactProjectionRequest request, CancellationToken cancellationToken)
    {
        if ((request.RollbackTargetTriggerKeys?.Length ?? 0) == 0 || string.IsNullOrWhiteSpace(request.RollbackOperationId)) return;
        try
        {
            if (_history is IRuntimeArtifactRollbackStatusRepository rollbackStatus)
                await rollbackStatus.TrySetRollbackStatus(request.RollbackOperationId, RuntimeOrchestrationArtifactStatus.Failed, cancellationToken);
        }
        catch (Exception statusException) when (statusException is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(statusException, "Could not record rollback outcome for {OperationId}.", request.RollbackOperationId);
        }
    }

    private async Task MarkProjectionFailedAsync(
        Id artifactId,
        long ingressGeneration,
        Exception exception,
        bool staged,
        CancellationToken cancellationToken)
    {
        try
        {
            using (_unitOfWork.DeferAutoSave())
            {
                if (staged && _artifactRepository is IRuntimeArtifactRolloutTransitionRepository transitions)
                    await transitions.TryMarkRolloutStandupFailed(artifactId, ingressGeneration, exception.Message, cancellationToken);
                else
                    await _artifactRepository.MarkProjectionFailed(artifactId, ingressGeneration, exception.Message, cancellationToken);
                await _unitOfWork.SaveChanges(cancellationToken);
            }
        }
        catch (Exception markException)
        {
            _logger.LogWarning(
                markException,
                "Could not mark artifact {ArtifactId} generation {IngressGeneration} projection as failed.",
                artifactId,
                ingressGeneration);
        }
    }
}
