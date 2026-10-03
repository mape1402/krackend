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

    /// <summary>
    /// Builds the schema context available before the specified stage starts.
    /// </summary>
    /// <param name="version">Complete orchestration version snapshot.</param>
    /// <param name="stageDefinitionId">Stage identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source schema context for the stage.</returns>
    Task<OrchestrationSchemaContext> BuildForStage(
        OrchestrationVersion version,
        Id stageDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema context available before a task compensation action is dispatched.
    /// </summary>
    /// <param name="version">Complete orchestration version snapshot.</param>
    /// <param name="taskDefinitionId">Forward task identifier that owns the compensation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for the task compensation.</returns>
    Task<OrchestrationSchemaContext> BuildForTaskCompensation(
        OrchestrationVersion version,
        Id taskDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema context available before trigger compensation is dispatched.
    /// </summary>
    /// <param name="version">Complete orchestration version snapshot.</param>
    /// <param name="triggerBindingId">Trigger binding identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source and target schema context for trigger compensation.</returns>
    Task<OrchestrationSchemaContext> BuildForTriggerCompensation(
        OrchestrationVersion version,
        Id triggerBindingId,
        CancellationToken cancellationToken = default);
}
