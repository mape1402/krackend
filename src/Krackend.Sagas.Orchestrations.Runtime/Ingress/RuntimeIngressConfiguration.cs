using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public sealed class RuntimeIngressConfiguration
    {
        public Id Id { get; set; }

        public Id RuntimeOrchestrationArtifactId { get; set; }

        public required string ConfigurationKey { get; set; }

        public IngressKind IngressKind { get; set; }

        public IngressTransport IngressTransport { get; set; }

        public required string SettingsPayload { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedOnUtc { get; set; }

        public DateTime UpdatedOnUtc { get; set; }

        public DateTime? DeactivatedOnUtc { get; set; }
    }
}
