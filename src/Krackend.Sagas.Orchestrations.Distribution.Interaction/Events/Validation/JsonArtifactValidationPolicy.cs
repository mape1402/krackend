using System.Text.Json;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class JsonArtifactValidationPolicy : IArtifactValidationPolicy
{
    public bool CanValidate(string artifactType)
        => string.Equals(artifactType, ArtifactTypes.OrchestrationVersionSnapshot, StringComparison.OrdinalIgnoreCase);

    public void Validate(Artifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (string.IsNullOrWhiteSpace(artifact.Payload))
        {
            throw new InvalidOperationException("Artifact payload is required.");
        }

        JsonDocument.Parse(artifact.Payload);
    }
}
