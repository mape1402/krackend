using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class DeprecatedArtifactBuilder : IArtifactBuilder<OrchestrationVersionDeprecatedEvent>
{
    public Artifact Build(OrchestrationVersionDeprecatedEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return ArtifactBuilderSupport.CreateArtifact(
            integrationEvent.OrchestrationVersionId,
            integrationEvent.OrchestrationDefinitionId,
            integrationEvent.OrchestrationDisplayName,
            integrationEvent.VersionLabel,
            integrationEvent.VersionNumber,
            integrationEvent.ArtifactPayloadJson,
            integrationEvent.Checksum,
            nameof(OrchestrationVersionDeprecatedEvent),
            integrationEvent.OccurredAtUtc);
    }
}
