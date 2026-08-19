using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class ArchivedArtifactBuilder : IArtifactBuilder<OrchestrationVersionArchivedEvent>
{
    public Artifact Build(OrchestrationVersionArchivedEvent integrationEvent)
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
            nameof(OrchestrationVersionArchivedEvent),
            integrationEvent.OccurredAtUtc);
    }
}
