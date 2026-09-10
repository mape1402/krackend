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
}
