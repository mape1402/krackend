namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Represents a permanent ingress projection failure caused by an invalid artifact configuration.
/// </summary>
public sealed class IngressProjectionConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IngressProjectionConfigurationException"/> class.
    /// </summary>
    /// <param name="message">Permanent projection configuration error message.</param>
    public IngressProjectionConfigurationException(string message)
        : base(message)
    {
    }
}
