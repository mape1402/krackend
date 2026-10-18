namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Signals that an adapter must reject an ingress message without starting a new SAGA.
/// </summary>
public sealed class IngressMessageRejectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IngressMessageRejectedException"/> class.
    /// </summary>
    public IngressMessageRejectedException(string message)
        : base(message)
    {
    }
}
