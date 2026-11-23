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

        /// <summary>Indicates preparation before rollout activation.</summary>
        public bool IsStaged { get; set; }

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

        /// <summary>
        /// Gets or sets how a matching message must be handled by the runtime.
        /// This value is projected into the transport registration so the hot
        /// consume path does not have to query runtime storage for every delivery.
        /// </summary>
        public IngressMessageDisposition MessageDisposition { get; set; } = IngressMessageDisposition.Process;

        /// <summary>Gets or sets the orchestration definition key that owns the route.</summary>
        public string OrchestrationDefinitionKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the orchestration version that owns the route.</summary>
        public string OrchestrationVersion { get; set; } = string.Empty;

        /// <summary>Gets or sets when the owning artifact was deployed.</summary>
        public DateTime DeployedOnUtc { get; set; }

        /// <summary>Gets or sets the original transport settings payload.</summary>
        public string SettingsPayload { get; set; } = string.Empty;
    }
}
