using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

internal static class ArtifactBuilderSupport
{
    public static Artifact CreateArtifact(
        string orchestrationVersionId,
        string orchestrationDefinitionId,
        string orchestrationDisplayName,
        string versionLabel,
        string versionNumber,
        string payload,
        string checksum,
        string sourceEvent,
        DateTime occurredAtUtc)
    {
        return new Artifact
        {
            Id = Id.New(),
            OrchestrationVersionId = orchestrationVersionId,
            OrchestrationDefinitionId = orchestrationDefinitionId,
            OrchestrationDisplayName = orchestrationDisplayName,
            VersionLabel = versionLabel,
            VersionNumber = versionNumber,
            ArtifactType = ArtifactTypes.OrchestrationVersionSnapshot,
            SchemaVersion = ArtifactTypes.SchemaVersion,
            Payload = payload,
            Metadata = JsonSerializer.Serialize(new
            {
                sourceEvent,
                occurredAtUtc
            }),
            SourceEvent = sourceEvent,
            SourceVersion = versionNumber,
            Checksum = checksum,
            IsPublished = false,
            CreatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = null
        };
    }
}
