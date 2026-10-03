using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class DefaultRuntimeArtifactSerializer : IRuntimeArtifactSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly IOrchestrationArtifactMigrator _migrator;

        public DefaultRuntimeArtifactSerializer()
            : this(new DefaultOrchestrationArtifactMigrator())
        {
        }

        public DefaultRuntimeArtifactSerializer(IOrchestrationArtifactMigrator migrator)
        {
            _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        }

        public OrchestrationArtifact Deserialize(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("Runtime artifact payload cannot be empty.", nameof(payload));
            }

            var artifact = JsonSerializer.Deserialize<OrchestrationArtifact>(payload, SerializerOptions)
                ?? throw new InvalidOperationException("Runtime artifact payload could not be deserialized.");

            return _migrator.Migrate(artifact);
        }
    }
}
