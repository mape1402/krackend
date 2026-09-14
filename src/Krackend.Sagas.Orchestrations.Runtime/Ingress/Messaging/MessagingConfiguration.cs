namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    /// <summary>
    /// Represents messaging-specific settings stored in an ingress configuration payload.
    /// </summary>
    public class MessagingConfiguration
    {
        /// <summary>
        /// Gets or sets the connector identifier used by the messaging adapter.
        /// </summary>
        public string ConnectorId { get; set; }

        /// <summary>
        /// Gets or sets the artifact identifier that owns the ingress configuration.
        /// </summary>
        public string ArtifactId { get; set; }

        /// <summary>
        /// Gets or sets the messaging topic consumed by the ingress.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the messaging contract version consumed by the ingress.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the ingress kind represented by the configuration.
        /// </summary>
        public IngressKind IngressKind { get; set; }

        /// <summary>
        /// Gets or sets the transport represented by the configuration.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }
    }
}
