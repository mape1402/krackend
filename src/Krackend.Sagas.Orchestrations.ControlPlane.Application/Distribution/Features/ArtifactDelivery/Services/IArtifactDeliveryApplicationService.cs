namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>
/// Coordinates artifact delivery from the control plane to runtime nodes.
/// </summary>
public interface IArtifactDeliveryApplicationService
{
    /// <summary>
    /// Pushes a release target artifact to its runtime node.
    /// </summary>
    Task<RuntimeArtifactDeliveryResult> Push(string releaseTargetId, string initiatedBy = "distribution", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns artifacts waiting for manual pull by a runtime node.
    /// </summary>
    Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingForPull(string runtimeNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one artifact package waiting for manual pull by a runtime node.
    /// </summary>
    Task<RuntimeArtifactDeliveryPackage> GetForPull(string runtimeNodeId, string releaseTargetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges a manually pulled artifact after the runtime installs it.
    /// </summary>
    Task<RuntimeArtifactDeliveryResult> AcknowledgePull(
        string runtimeNodeId,
        string releaseTargetId,
        string runtimeArtifactId,
        string runtimeArtifactStatus,
        CancellationToken cancellationToken = default);
}
