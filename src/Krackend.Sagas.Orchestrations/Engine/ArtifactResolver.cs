using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

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
}
