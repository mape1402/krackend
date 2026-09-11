namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Represents a transport-specific ingress endpoint ready to be started by a runtime replica.
    /// </summary>
    public class IngressConfiguration
    {
        /// <summary>
        /// Gets or sets the runtime ingress configuration id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the runtime artifact id that owns this ingress.
        /// </summary>
        public string ArtifactId { get; set; }

        /// <summary>
        /// Gets or sets the orchestration definition key that owns the artifact.
        /// </summary>
        public string OrchestrationDefinitionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the orchestration artifact version that owns this ingress.
        /// </summary>
        public string OrchestrationVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the owning runtime artifact was deployed in UTC.
        /// </summary>
        public DateTime DeployedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the transport used by this ingress.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets or sets the transport-specific serialized settings.
        /// </summary>
        public string SettingsPayload { get; set; }

        /// <summary>
        /// Gets or sets whether this ingress starts or continues an orchestration.
        /// </summary>
        public IngressKind IngressKind { get; set; }
    }
}
