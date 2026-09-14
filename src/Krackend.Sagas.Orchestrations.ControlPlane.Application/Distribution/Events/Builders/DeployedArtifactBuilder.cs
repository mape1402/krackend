using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class DeployedArtifactBuilder : IArtifactBuilder<OrchestrationVersionDeployedEvent>
{
    private readonly IArtifactFactory _artifactFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeployedArtifactBuilder"/> class.
    /// </summary>
    /// <param name="artifactFactory">Artifact factory.</param>
    public DeployedArtifactBuilder(IArtifactFactory artifactFactory)
    {
        _artifactFactory = artifactFactory ?? throw new ArgumentNullException(nameof(artifactFactory));
    }

    /// <inheritdoc />
    public Artifact Build(OrchestrationVersionDeployedEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _artifactFactory.Create(
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
