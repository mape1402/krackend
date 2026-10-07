namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Configures automated runtime reconciliation.
/// </summary>
public sealed class RuntimeReconciliationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether reconciliation runs in the background.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the background scan interval in seconds.
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum number of recoverable instances processed per scan.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
