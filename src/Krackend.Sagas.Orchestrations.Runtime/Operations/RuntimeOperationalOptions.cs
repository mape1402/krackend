namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Configures runtime operational monitoring and admission.
/// </summary>
public sealed class RuntimeOperationalOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether background dependency monitoring is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the background dependency scan interval in seconds.
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 5;
}
