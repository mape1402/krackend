namespace Krackend.Sagas.Orchestrations.Runtime;

/// <summary>
/// Configures runtime-wide behavior.
/// </summary>
public sealed class RuntimeOptions
{
    /// <summary>
    /// Gets or sets the runtime environment key.
    /// </summary>
    public string EnvironmentKey { get; set; } = "local";
}
