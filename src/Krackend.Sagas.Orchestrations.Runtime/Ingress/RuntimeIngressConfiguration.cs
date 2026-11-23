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
        /// Gets or sets how the owning artifact wants matching ingress messages to be settled.
        /// </summary>
        public IngressMessageDisposition MessageDisposition { get; set; } = IngressMessageDisposition.Process;

        /// <summary>
        /// Gets or sets the transport used by this ingress.
        /// </summary>
        public IngressTransport IngressTransport { get; set; }

        /// <summary>
        /// Gets or sets the transport-specific serialized settings.
        /// </summary>
        public required string SettingsPayload { get; set; }

        /// <summary>Gets or sets the normalized messaging endpoint used by the selective route index.</summary>
        public string RouteTopic { get; set; }

        /// <summary>Gets or sets the normalized messaging version used by the selective route index.</summary>
        public string RouteVersion { get; set; }

        /// <summary>Projects the transport settings once when a route is written.</summary>
        public void ProjectMessagingRoute()
        {
            RouteTopic = RouteVersion = null;
            if (IngressTransport != IngressTransport.Messaging) return;
            try
            {
                using var settings = System.Text.Json.JsonDocument.Parse(SettingsPayload);
                if (settings.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return;
                if (settings.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return;
                if (settings.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return;
                foreach (var property in settings.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                    if (string.Equals(property.Name, "topic", StringComparison.OrdinalIgnoreCase))
                        RouteTopic = property.Value.GetString()?.ToUpperInvariant();
                    else if (string.Equals(property.Name, "version", StringComparison.OrdinalIgnoreCase))
                        RouteVersion = property.Value.GetString()?.ToUpperInvariant();
                }
            }
            catch (System.Text.Json.JsonException) { /* Legacy invalid settings retain the normal selector's rejection. */ }
        }

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
