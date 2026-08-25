using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Projects runtime artifact ingress configuration immediately when no durable scheduler is configured.
/// </summary>
internal sealed class ImmediateRuntimeArtifactProjectionScheduler : IRuntimeArtifactProjectionScheduler
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IRuntimeIngressConfigurationProjector _projector;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IRuntimeArtifactReadyNotifier _artifactReadyNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmediateRuntimeArtifactProjectionScheduler"/> class.
    /// </summary>
    public ImmediateRuntimeArtifactProjectionScheduler(
        IRuntimeArtifactRepository artifactRepository,
        IRuntimeIngressConfigurationProjector projector,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeArtifactReadyNotifier artifactReadyNotifier)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _artifactReadyNotifier = artifactReadyNotifier ?? throw new ArgumentNullException(nameof(artifactReadyNotifier));
    }

    /// <inheritdoc />
    public async Task ScheduleProjectionAsync(
        RuntimeArtifactProjectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var artifactId = new Id(Ulid.Parse(request.ArtifactId));
        var artifact = await _artifactRepository.GetById(artifactId, cancellationToken);
        if (artifact.IngressGeneration != request.IngressGeneration)
        {
            return;
        }

        if (artifact.Status != RuntimeOrchestrationArtifactStatus.Ready)
        {
            try
            {
                using (_unitOfWork.DeferAutoSave())
                {
                    await _artifactRepository.MarkProjectionStarted(
                        artifactId,
                        request.IngressGeneration,
                        cancellationToken);
                    await _projector.ProjectAsync(artifact, cancellationToken);
                    await _artifactRepository.MarkReady(artifactId, request.IngressGeneration, cancellationToken);
                    await _unitOfWork.SaveChanges(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                using (_unitOfWork.DeferAutoSave())
                {
                    await _artifactRepository.MarkProjectionFailed(
                        artifactId,
                        request.IngressGeneration,
                        exception.Message,
                        cancellationToken);
                    await _unitOfWork.SaveChanges(cancellationToken);
                }

                throw;
            }
        }

        var readyArtifact = await _artifactRepository.GetById(artifactId, cancellationToken);
        await _artifactReadyNotifier.NotifyReadyAsync(
            RuntimeArtifactReadyGossipMessage.FromArtifact(readyArtifact),
            cancellationToken);
    }
}
