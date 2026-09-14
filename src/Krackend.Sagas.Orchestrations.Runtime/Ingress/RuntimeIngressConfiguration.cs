using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Stores a projected ingress configuration for a runtime artifact.
    /// </summary>
    public sealed class RuntimeIngressConfiguration
    {
        /// <summary>
        /// Gets or sets the ingress configuration id.
        /// </summary>
        public Id Id { get; set; }

        /// <summary>
        /// Gets or sets the runtime artifact id that owns this ingress.
        /// </summary>
        public Id RuntimeOrchestrationArtifactId { get; set; }

        /// <summary>
        /// Gets or sets the deterministic configuration key for idempotent projection.
        /// </summary>
        public required string ConfigurationKey { get; set; }

        /// <summary>
        /// Gets or sets whether this ingress starts or continues an orchestration.
        /// </summary>
        public IngressKind IngressKind { get; set; }

        /// <summary>
        /// Gets or sets the transport used by this ingress.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets or sets the transport-specific serialized settings.
        /// </summary>
        public required string SettingsPayload { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this ingress configuration is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets when the ingress configuration was created in UTC.
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets when the ingress configuration was last updated in UTC.
        /// </summary>
        public DateTime UpdatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets when the ingress configuration was deactivated in UTC.
        /// </summary>
        public DateTime? DeactivatedOnUtc { get; set; }
    }
}
