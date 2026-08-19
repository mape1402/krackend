using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class DeployedArtifactBuilder : IArtifactBuilder<OrchestrationVersionDeployedEvent>
{
    public Artifact Build(OrchestrationVersionDeployedEvent integrationEvent)
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
            nameof(OrchestrationVersionDeployedEvent),
            integrationEvent.OccurredAtUtc);
    }
}
