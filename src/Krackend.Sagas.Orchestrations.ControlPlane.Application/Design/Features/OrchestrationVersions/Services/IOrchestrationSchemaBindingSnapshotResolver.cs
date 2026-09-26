using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Resolves schema snapshots for schema bindings before bindings are stored or artifacts are published.
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

    /// <summary>
    /// Resolves missing schema snapshots in the specified trigger binding.
    /// </summary>
    /// <param name="trigger">Trigger binding to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResolveTriggerAsync(TriggerBinding trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves missing schema snapshots in the specified task definition.
    /// </summary>
    /// <param name="task">Task definition to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResolveTaskAsync(TaskDefinition task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves missing schema snapshots in the specified compensation definition.
    /// </summary>
    /// <param name="compensation">Compensation definition to enrich.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResolveCompensationAsync(CompensationDefinition compensation, CancellationToken cancellationToken = default);
}
