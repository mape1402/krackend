using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Builds the compensation plan for a failed orchestration instance.
/// </summary>
internal interface IRuntimeCompensationPlanBuilder
{
    /// <summary>
    /// Builds compensation plan items for completed task executions.
    /// </summary>
    /// <param name="document">Runtime artifact document.</param>
    /// <param name="completedTasks">Completed task executions.</param>
    /// <returns>Compensation plan in execution order.</returns>
    IReadOnlyCollection<RuntimeCompensationPlanItem> Build(
        RuntimeArtifactDocument document,
        IReadOnlyCollection<TaskExecution> completedTasks);
}
