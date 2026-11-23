using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists stage execution state.
/// </summary>
public interface IStageExecutionRepository
{
    Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default);

    Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default);

    Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default);

    Task<StageExecution> GetByInstanceAndKey(
        Id instanceId,
        string stageKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the stage list and one stage's task list in one provider operation.
/// </summary>
/// <remarks>
/// The callback hot path needs both collections to evaluate the next decision.
/// Keeping this capability optional preserves compatibility with storage adapters
/// that do not support a joined execution-graph query.
/// </remarks>
public interface IOrchestrationExecutionGraphReader
{
    /// <summary>Gets every stage plus the tasks belonging to the selected stage.</summary>
    Task<OrchestrationExecutionGraph> GetExecutionGraph(
        Id instanceId,
        Id stageExecutionId,
        CancellationToken cancellationToken = default);
}

/// <summary>The execution rows needed to advance one callback.</summary>
public sealed record OrchestrationExecutionGraph(
    IReadOnlyCollection<StageExecution> Stages,
    IReadOnlyCollection<TaskExecution> StageTasks);
