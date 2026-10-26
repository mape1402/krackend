namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Processes durable observations into the independent statistics projection.</summary>
public interface ISagaStatisticsBatchProcessor
{
    /// <summary>Processes up to the requested number of observations.</summary>
    Task<SagaStatisticsBatchResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>Describes the outcome of one statistics batch.</summary>
public sealed record SagaStatisticsBatchResult(int Examined, int Applied, int Deferred);
