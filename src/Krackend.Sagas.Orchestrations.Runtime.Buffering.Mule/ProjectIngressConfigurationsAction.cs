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

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectIngressConfigurationsAction"/> class.
    /// </summary>
    public ProjectIngressConfigurationsAction(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeIngressConfigurationProjector projector,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactReadyNotifier artifactReadyNotifier,
        ILogger<ProjectIngressConfigurationsAction> logger,
        IMuleTerminalFailureMarker terminalFailureMarker)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _artifactReadyNotifier = artifactReadyNotifier ?? throw new ArgumentNullException(nameof(artifactReadyNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _terminalFailureMarker = terminalFailureMarker ?? throw new ArgumentNullException(nameof(terminalFailureMarker));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        MuleActionContext<RuntimeArtifactProjectionRequest> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Payload ?? throw new InvalidOperationException("Projection request payload is required.");
        var artifactId = new Id(Ulid.Parse(request.ArtifactId));
        var artifact = await _artifactRepository.GetById(artifactId, cancellationToken);
        if (artifact.IngressGeneration != request.IngressGeneration)
        {
            _logger.LogInformation(
                "Skipping stale ingress projection for artifact {ArtifactId}. Requested generation {RequestedGeneration}, current generation {CurrentGeneration}.",
                request.ArtifactId,
                request.IngressGeneration,
                artifact.IngressGeneration);
            return;
        }

        try
        {
            if (artifact.Status != RuntimeOrchestrationArtifactStatus.Ready)
            {
                artifact = await ProjectIngressConfigurationsAsync(request, artifact, artifactId, cancellationToken);
            }

            await _artifactReadyNotifier.NotifyReadyAsync(
                RuntimeArtifactReadyGossipMessage.FromArtifact(artifact),
                cancellationToken);
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
                await _artifactRepository.MarkProjectionStarted(
                    artifactId,
                    request.IngressGeneration,
                    cancellationToken);
                await _projector.ProjectAsync(artifact, cancellationToken);
                await _artifactRepository.MarkReady(
                    artifactId,
                    request.IngressGeneration,
                    cancellationToken);
                await _unitOfWork.SaveChanges(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return await _artifactRepository.GetById(artifactId, cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkProjectionFailedAsync(artifactId, request.IngressGeneration, exception, cancellationToken);
            throw;
        }
    }

    private async Task MarkProjectionFailedAsync(
        Id artifactId,
        long ingressGeneration,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            using (_unitOfWork.DeferAutoSave())
            {
                await _artifactRepository.MarkProjectionFailed(
                    artifactId,
                    ingressGeneration,
                    exception.Message,
                    cancellationToken);
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
