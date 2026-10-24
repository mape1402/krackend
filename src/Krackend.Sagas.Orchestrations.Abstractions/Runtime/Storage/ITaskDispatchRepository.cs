using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists outbound task dispatch records.
/// </summary>
public interface ITaskDispatchRepository
{
    Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default);

    Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default);

    Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default);

    Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dispatches whose durable delivery intent was persisted but has not yet been acknowledged by its execution provider.
    /// </summary>
    /// <param name="take">Maximum number of dispatches to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending durable delivery intents.</returns>
    Task<IReadOnlyCollection<TaskDispatch>> GetQueuePending(int take, CancellationToken cancellationToken = default);
}
