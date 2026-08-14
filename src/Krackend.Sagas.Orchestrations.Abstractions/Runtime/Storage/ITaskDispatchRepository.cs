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

    Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default);
}
