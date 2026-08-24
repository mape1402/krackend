using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Default runtime artifact installer.
/// </summary>
public sealed class RuntimeArtifactDeploymentService : IRuntimeArtifactDeploymentService
{
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IIngressRegistry _ingressRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactDeploymentService"/> class.
    /// </summary>
    public RuntimeArtifactDeploymentService(
        IRuntimeArtifactRepository artifactRepository,
        IIngressRegistry ingressRegistry)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _ingressRegistry = ingressRegistry ?? throw new ArgumentNullException(nameof(ingressRegistry));
    }

    /// <inheritdoc />
    public async Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var version = ParseVersion(package.Version);
        var artifactId = ParseId(package.ArtifactId);
        var existingArtifact = await TryGetExistingArtifact(
            package.EnvironmentKey,
            package.OrchestrationDefinitionKey,
            version,
            cancellationToken);

        var now = DateTime.UtcNow;
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = existingArtifact?.Id ?? artifactId,
            EnvironmentKey = package.EnvironmentKey,
            OrchestrationDefinitionKey = package.OrchestrationDefinitionKey,
            ArtifactType = package.ArtifactType,
            SourceOrchestrationVersionId = ParseId(package.OrchestrationVersionId),
            Version = version,
            ArtifactChecksum = new Checksum(package.Checksum),
            ArtifactPayload = JsonNode.Parse(package.PayloadJson)
                ?? throw new InvalidOperationException("Artifact payload is empty."),
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = existingArtifact?.DeployedOnUtc ?? now,
            ActivatedOnUtc = now,
            RetiredOnUtc = null,
            SupersededByArtifactId = null,
            Notes = $"Installed from '{sourceKey}' release target '{package.ReleaseTargetId}'."
        };

        await _artifactRepository.Upsert(artifact, cancellationToken);
        await _ingressRegistry.StandUpOneAsync(artifact.Id.ToString(), cancellationToken);

        return new RuntimeArtifactDeploymentResult
        {
            Accepted = true,
            RuntimeArtifactId = artifact.Id.ToString(),
            Status = "Activated",
            Message = $"Artifact '{artifact.OrchestrationDefinitionKey}' v{artifact.Version} installed."
        };
    }

    private async Task<RuntimeOrchestrationArtifact> TryGetExistingArtifact(
        string environmentKey,
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _artifactRepository.GetByVersion(
                environmentKey,
                orchestrationDefinitionKey,
                version,
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = (value ?? string.Empty).Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException($"Semantic version '{value}' is invalid.");
        }

        return new SemanticVersion(major, minor, patch);
    }
}
