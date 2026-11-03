using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Optional batch reads of attempts for task executions.</summary>
public interface ITaskExecutionAttemptBatchReader
{
    Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionIds(
        IReadOnlyCollection<Id> taskExecutionIds, CancellationToken cancellationToken = default);
}
