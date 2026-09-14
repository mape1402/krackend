namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Identifies the transport adapter used by an ingress endpoint.
    /// </summary>
    public enum IngressTransport
    {
        /// <summary>
        /// Messaging-based ingress.
        /// </summary>
        Messaging,

        /// <summary>
        /// HTTP-based ingress.
        /// </summary>
        Http
    }
}
