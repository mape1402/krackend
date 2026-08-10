namespace Krackend.Sagas.Orchestrations.Runtime;

/// <summary>
/// Describes the runtime environment configured for this host.
/// </summary>
public sealed class RuntimeEnvironmentDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeEnvironmentDescriptor"/> class.
    /// </summary>
    /// <param name="environmentKey">Runtime environment key.</param>
    public RuntimeEnvironmentDescriptor(string environmentKey)
    {
        EnvironmentKey = environmentKey;
    }

    /// <summary>
    /// Gets the runtime environment key.
    /// </summary>
    public string EnvironmentKey { get; }
}
