namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IArtifactDeliveryApplicationService
{
    Task<RuntimeArtifactDeliveryResult> Push(string releaseTargetId, string initiatedBy = "distribution", CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingForPull(string runtimeNodeId, CancellationToken cancellationToken = default);
    Task<RuntimeArtifactDeliveryResult> AcknowledgePull(string runtimeNodeId, string releaseTargetId, string runtimeArtifactId, CancellationToken cancellationToken = default);
}
