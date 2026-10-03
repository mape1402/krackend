namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides design-time schema contexts for orchestration tasks.
/// </summary>
public interface IOrchestrationSchemaContextApplicationService
{
    /// <summary>
    /// Gets the schema context available before the requested task is dispatched.
    /// </summary>
    /// <param name="query">Schema context query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for the task.</returns>
    Task<OrchestrationSchemaContext> GetForTask(
        GetTaskSchemaContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema context available before the requested stage starts.
    /// </summary>
    /// <param name="query">Schema context query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source schema context for the stage.</returns>
    Task<OrchestrationSchemaContext> GetForStage(
        GetStageSchemaContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema context available before the requested task compensation is dispatched.
    /// </summary>
    /// <param name="query">Schema context query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for the task compensation.</returns>
    Task<OrchestrationSchemaContext> GetForTaskCompensation(
        GetTaskCompensationSchemaContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema context available before the requested trigger compensation is dispatched.
    /// </summary>
    /// <param name="query">Schema context query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for trigger compensation.</returns>
    Task<OrchestrationSchemaContext> GetForTriggerCompensation(
        GetTriggerCompensationSchemaContextQuery query,
        CancellationToken cancellationToken = default);
}
