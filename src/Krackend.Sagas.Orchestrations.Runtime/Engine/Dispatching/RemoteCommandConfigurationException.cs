namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Represents a permanent runtime configuration error that prevents a remote command from being dispatched.
/// </summary>
public sealed class RemoteCommandConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteCommandConfigurationException"/> class.
    /// </summary>
    /// <param name="message">Configuration error message.</param>
    public RemoteCommandConfigurationException(string message)
        : base(message)
    {
    }
}
