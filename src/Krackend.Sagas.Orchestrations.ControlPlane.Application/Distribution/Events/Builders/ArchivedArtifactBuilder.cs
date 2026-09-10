using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ArchivedArtifactBuilder : IArtifactBuilder<OrchestrationVersionArchivedEvent>
{
    private readonly IArtifactFactory _artifactFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchivedArtifactBuilder"/> class.
    /// </summary>
    /// <param name="artifactFactory">Artifact factory.</param>
    public ArchivedArtifactBuilder(IArtifactFactory artifactFactory)
    {
        _artifactFactory = artifactFactory ?? throw new ArgumentNullException(nameof(artifactFactory));
    }

    /// <inheritdoc />
    public Artifact Build(OrchestrationVersionArchivedEvent integrationEvent)
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
            nameof(OrchestrationVersionArchivedEvent),
            integrationEvent.OccurredAtUtc);
    }
}
