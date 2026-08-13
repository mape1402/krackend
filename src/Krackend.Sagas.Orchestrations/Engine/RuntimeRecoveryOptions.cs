namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Configures runtime pending work recovery.
/// </summary>
public sealed class RuntimeRecoveryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the background recovery loop is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether pending work must be scanned once when the host starts.
    /// </summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between pending work scans.
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the minimum allowed scan interval.
    /// </summary>
    public TimeSpan MinimumScanInterval { get; set; } = TimeSpan.FromSeconds(1);
}
