namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Represents a delivery failure where the provider cannot determine whether it accepted the command.
/// </summary>
/// <remarks>
/// The runtime retains the durable dispatch intent when this exception is raised. A recovery pass can
/// safely resubmit the same dispatch identifier through providers that support idempotent delivery.
/// </remarks>
public sealed class RemoteCommandDeliveryUncertainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteCommandDeliveryUncertainException"/> class.
    /// </summary>
    /// <param name="message">The provider failure description.</param>
    /// <param name="innerException">The underlying provider exception.</param>
    public RemoteCommandDeliveryUncertainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
