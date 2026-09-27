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

    /// <summary>
    /// Records the instant when a dispatch was sent without changing its current status.
    /// </summary>
    /// <param name="dispatchId">Dispatch identifier.</param>
    /// <param name="sentOnUtc">UTC instant when the dispatch was sent.</param>
    /// <param name="externalReference">Optional transport-specific publish reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the dispatch has been updated.</returns>
    Task RecordSent(Id dispatchId, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default);

    Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default);
}
