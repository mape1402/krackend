using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists task execution state.
/// </summary>
public interface ITaskExecutionRepository
{
    Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default);

    Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default);

    Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default);

    Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default);

    Task<TaskExecution> GetByStageAndKey(
        Id stageExecutionId,
        string taskKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks still waiting for response whose waiting timestamp is older than or equal to the provided cutoff.
    /// </summary>
    /// <param name="dueBeforeUtc">Maximum waiting timestamp to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Waiting response task executions.</returns>
    Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(
        DateTime dueBeforeUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a bounded page of response waits whose persisted deadline has elapsed.
    /// Implementations that have not yet persisted deadlines may fall back to the legacy waiting timestamp query.
    /// </summary>
    Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsDue(
        DateTime dueBeforeUtc,
        int take = 100,
        CancellationToken cancellationToken = default)
        => GetWaitingResponseOlderThan(dueBeforeUtc, cancellationToken);

    /// <summary>
    /// Gets awaited response tasks whose durable timeout action reference was not persisted, so a bounded repair
    /// process can restore only inconsistent waits rather than scan all healthy instances.
    /// </summary>
    Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsMissingAction(
        int take = 100,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<TaskExecution>>(Array.Empty<TaskExecution>());
}

/// <summary>Optional bounded paging for timeout repair, so inconsistent rows cannot block later work.</summary>
public interface IResponseTimeoutRepairPageReader
{
    /// <summary>Reads a page of overdue response waits in stable deadline order.</summary>
    Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsDuePage(
        DateTime dueBeforeUtc, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Reads a page of response waits without a captured timer.</summary>
    Task<IReadOnlyCollection<TaskExecution>> GetResponseTimeoutsMissingActionPage(
        int skip, int take, CancellationToken cancellationToken = default);
}
