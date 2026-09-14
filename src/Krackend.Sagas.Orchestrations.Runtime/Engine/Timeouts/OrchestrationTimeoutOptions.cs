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
}
