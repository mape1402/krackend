namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

/// <summary>Configures bounded cleanup of old successful runtime execution graphs.</summary>
public sealed class RuntimeHistoryRetentionOptions
{
    /// <summary>Gets or sets whether automatic history retention is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets how long completed and compensated instances remain queryable.
    /// Failed, dead-lettered, stopped and aborted instances are never removed by this policy.
    /// </summary>
    public int CompletedRetentionHours { get; set; } = 168;

    /// <summary>Gets or sets the maximum execution graphs removed by one database batch.</summary>
    public int BatchSize { get; set; } = 250;

    /// <summary>Gets or sets the maximum batches processed in one low-priority sweep.</summary>
    public int MaxBatchesPerSweep { get; set; } = 4;

    /// <summary>Gets or sets the delay before the first cleanup after host startup.</summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>Gets or sets the delay between cleanup sweeps.</summary>
    public int SweepIntervalSeconds { get; set; } = 300;
}

/// <summary>Removes expired successful execution graphs without touching active or failed sagas.</summary>
public interface IRuntimeHistoryRetentionProcessor
{
    /// <summary>Processes one bounded cleanup batch.</summary>
    Task<RuntimeHistoryRetentionResult> ProcessAsync(
        DateTime completedBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes one bounded retention batch.</summary>
public sealed record RuntimeHistoryRetentionResult(int CandidateInstances, int RemovedInstances, int RemovedRows);
