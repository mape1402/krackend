using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Engine;

public sealed class ArtifactResolver : IArtifactResolver
{
    private readonly IRuntimeArtifactRepository _artifactRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactResolver"/> class.
    /// </summary>
    /// <param name="artifactRepository">Repository used to read promoted runtime artifacts.</param>
    public ArtifactResolver(IRuntimeArtifactRepository artifactRepository)
    {
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
    }

    /// <inheritdoc/>
    public async Task<RuntimeOrchestrationArtifact> ResolveActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationDefinitionKey))
            throw new InvalidOperationException("Trigger key is required to resolve an active artifact.");

        var artifact = await _artifactRepository.GetActive(environmentKey, orchestrationDefinitionKey, cancellationToken);

        return artifact ?? throw new InvalidOperationException($"No active runtime artifact found for '{orchestrationDefinitionKey}' in environment '{environmentKey}'.");
    }

    /// <inheritdoc/>
    public async Task<RuntimeOrchestrationArtifact> Resolve(
        string environmentKey,
        string orchestrationDefinitionKey,
        string artifactVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactVersion))
            return await ResolveActive(environmentKey, orchestrationDefinitionKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(orchestrationDefinitionKey))
            throw new InvalidOperationException("Trigger key is required to resolve a versioned artifact.");

        var version = ParseVersion(artifactVersion);
        var artifact = await _artifactRepository.GetByVersion(
            environmentKey,
            orchestrationDefinitionKey,
            version,
            cancellationToken);

        return artifact ?? throw new InvalidOperationException($"No runtime artifact version '{version}' found for '{orchestrationDefinitionKey}' in environment '{environmentKey}'.");
    }

    /// <inheritdoc/>
    public async Task<RuntimeOrchestrationArtifact> ResolveById(
        Id artifactId,
        CancellationToken cancellationToken = default)
    {
        if (artifactId == default)
            throw new InvalidOperationException("Artifact id is required.");

        return await _artifactRepository.GetById(artifactId, cancellationToken);
    }

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = value.Trim().Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException($"Artifact version '{value}' is not a semantic version.");
        }

        return new SemanticVersion(major, minor, patch);
    }
}
