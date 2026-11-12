using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Optional batched attempt summaries without request, response, metadata, or dispatch identifiers.</summary>
public interface ITaskExecutionAttemptSummaryBatchReader
{
    /// <summary>Reads summary fields for all attempts of the supplied tasks.</summary>
    Task<IReadOnlyCollection<TaskExecutionAttempt>> GetSummariesByTaskExecutionIds(
        IReadOnlyCollection<Id> taskExecutionIds, CancellationToken cancellationToken = default);
}
