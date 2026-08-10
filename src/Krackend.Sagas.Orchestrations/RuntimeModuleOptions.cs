namespace Krackend.Sagas.Orchestrations.Runtime;

/// <summary>
/// Defines minimal runtime module options.
/// </summary>
public sealed class RuntimeModuleOptions
{
    /// <summary>
    /// Gets or sets the environment key owned by this runtime host.
    /// </summary>
    public string EnvironmentKey { get; set; } = "local";
}
