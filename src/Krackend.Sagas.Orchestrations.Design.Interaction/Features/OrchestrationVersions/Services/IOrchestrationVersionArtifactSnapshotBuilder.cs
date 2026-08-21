using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Builds complete orchestration version snapshots ready for artifact serialization.
/// </summary>
public interface IOrchestrationVersionArtifactSnapshotBuilder
{
    /// <summary>
    /// Builds a complete snapshot of the specified orchestration version.
    /// </summary>
    /// <param name="version">Version to snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete orchestration version snapshot.</returns>
    Task<OrchestrationVersion> Build(
        OrchestrationVersion version,
        CancellationToken cancellationToken = default);
}
