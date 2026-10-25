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

    /// <summary>Returns a page of pending releases from one control plane.</summary>
    Task<PendingArtifactPage> GetPendingPageAsync(string sourceKey, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured pull client does not support paged pending releases.");

    /// <summary>
    /// Pulls and installs one release target from one source.
    /// </summary>
    Task<RuntimeArtifactDeploymentResult> ApplyAsync(
        string sourceKey,
        string releaseTargetId,
        CancellationToken cancellationToken = default);
}
