namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Instructs a transport adapter how to settle an ingress message selected by the runtime.
/// </summary>
public enum IngressMessageDisposition
{
    /// <summary>
    /// The ingress may process the message normally.
    /// </summary>
    Process,

    /// <summary>
    /// The ingress must reject a new trigger message using its transport-specific dead-letter mechanism.
    /// </summary>
    Reject
}
