namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Builds the schema context available to a task at design time.
/// </summary>
public interface IOrchestrationSchemaContextBuilder
{
    /// <summary>
    /// Builds the schema context available before the specified task is dispatched.
    /// </summary>
    /// <param name="version">Complete orchestration version snapshot.</param>
    /// <param name="taskDefinitionId">Task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for the task.</returns>
    Task<OrchestrationSchemaContext> BuildForTask(
        OrchestrationVersion version,
        Id taskDefinitionId,
        CancellationToken cancellationToken = default);
}
