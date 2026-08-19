using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Synchronizes runtime artifact ingresses with the current host.
/// </summary>
public interface IRuntimeIngressSynchronizer
{
    /// <summary>
    /// Synchronizes all active artifacts using paged storage reads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SynchronizeActiveArtifacts(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes one promoted or lifecycle artifact while the host is running.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SynchronizeArtifact(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);
}
