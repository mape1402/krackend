namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>
/// Configures the runtime timeout scanner for orchestration tasks waiting on a response.
/// </summary>
public sealed class OrchestrationTimeoutOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the timeout scanner is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval, in seconds, between timeout scans.
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of overdue waits considered by one recovery scan.
    /// The scanner still yields between candidates when realtime ingress is active.
    /// </summary>
    public int RecoveryBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of unrelated orchestration instances that bulk
    /// timeout recovery may repair concurrently.
    /// </summary>
    public int RecoveryMaxDegreeOfParallelism { get; set; } = 8;

    /// <summary>
    /// Gets or sets how long bulk recovery waits after realtime ingress becomes quiet.
    /// This protects foreground latency without reducing foreground parallelism.
    /// </summary>
    public int RecoveryQuietPeriodMilliseconds { get; set; } = 250;

    /// <summary>Default maximum lifetime in seconds. Zero disables the default.</summary>
    public int DefaultInstanceLifetimeSeconds { get; set; } = 86400;

    /// <summary>Default idle reconciliation window in seconds.</summary>
    public int DefaultIdleDurationSeconds { get; set; } = 1200;

    /// <summary>Interval between scans for instances whose maximum lifetime elapsed.</summary>
    public int InstanceLifetimeScanIntervalSeconds { get; set; } = 86400;

    /// <summary>Maximum number of candidates read in one page.</summary>
    public int InstanceLifetimeBatchSize { get; set; } = 100;
}
