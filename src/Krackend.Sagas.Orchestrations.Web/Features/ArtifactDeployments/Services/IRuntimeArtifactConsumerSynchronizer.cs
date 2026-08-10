using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Synchronizes deployed runtime artifacts with active message consumers.
/// </summary>
public interface IRuntimeArtifactConsumerSynchronizer
{
    /// <summary>
    /// Applies consumer changes required by an artifact lifecycle event.
    /// </summary>
    /// <param name="artifact">Runtime artifact that was materialized.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Synchronize(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default);
}
