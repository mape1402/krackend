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

    /// <summary>Default maximum lifetime in seconds. Zero disables the default.</summary>
    public int DefaultInstanceLifetimeSeconds { get; set; } = 86400;

    /// <summary>Default idle reconciliation window in seconds.</summary>
    public int DefaultIdleDurationSeconds { get; set; } = 1200;

    /// <summary>Interval between scans for instances whose maximum lifetime elapsed.</summary>
    public int InstanceLifetimeScanIntervalSeconds { get; set; } = 86400;

    /// <summary>Maximum number of candidates read in one page.</summary>
    public int InstanceLifetimeBatchSize { get; set; } = 100;
}
