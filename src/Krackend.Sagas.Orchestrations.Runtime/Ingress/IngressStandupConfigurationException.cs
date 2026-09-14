namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Represents a permanent ingress standup failure caused by missing runtime configuration.
/// </summary>
public sealed class IngressStandupConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IngressStandupConfigurationException"/> class.
    /// </summary>
    /// <param name="message">Permanent configuration error message.</param>
    public IngressStandupConfigurationException(string message)
        : base(message)
    {
    }
}
