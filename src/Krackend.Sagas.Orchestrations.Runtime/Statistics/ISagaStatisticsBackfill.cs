namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Captures a baseline for instances created before statistics were enabled.</summary>
public interface ISagaStatisticsBackfill
{
    /// <summary>Processes a bounded set of legacy instances, returning true when backfill is complete.</summary>
    Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default);
}
