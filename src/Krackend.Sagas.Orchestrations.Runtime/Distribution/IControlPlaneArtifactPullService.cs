using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Pulls available artifact releases from registered control-plane sources.
/// </summary>
public interface IControlPlaneArtifactPullService
{
    /// <summary>
    /// Returns registered control-plane sources.
    /// </summary>
    IReadOnlyCollection<ControlPlaneDistributionSource> GetSources();

    /// <summary>
    /// Returns packages available for manual pull from one source.
    /// </summary>
    Task<IReadOnlyCollection<RuntimeArtifactDeliveryPackage>> GetPendingAsync(
        string sourceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls and installs one release target from one source.
    /// </summary>
    Task<RuntimeArtifactDeploymentResult> ApplyAsync(
        string sourceKey,
        string releaseTargetId,
        CancellationToken cancellationToken = default);
}
