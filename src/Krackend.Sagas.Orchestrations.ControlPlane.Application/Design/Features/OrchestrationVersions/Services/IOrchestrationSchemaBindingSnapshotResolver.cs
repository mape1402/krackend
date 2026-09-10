using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Resolves schema snapshots for schema bindings before an orchestration artifact is published.
/// </summary>
public interface IOrchestrationSchemaBindingSnapshotResolver
{
    /// <summary>
    /// Resolves missing schema snapshots in the specified orchestration version.
    /// </summary>
    /// <param name="version">Orchestration version to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResolveAsync(OrchestrationVersion version, CancellationToken cancellationToken = default);
}
