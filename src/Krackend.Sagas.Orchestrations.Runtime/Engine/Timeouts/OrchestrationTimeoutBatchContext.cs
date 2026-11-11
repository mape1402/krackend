using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>Stores artifact lookups for one sequential timeout repair pass.</summary>
public sealed class OrchestrationTimeoutBatchContext
{
    private readonly Dictionary<Id, ResolvedOrchestrationArtifact> _artifacts = new();
    private readonly HashSet<Id> _missing = new();

    internal async Task<ResolvedOrchestrationArtifact> ResolveAsync(
        Id id, IRuntimeArtifactResolver resolver, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_artifacts.TryGetValue(id, out var artifact)) return artifact;
        if (_missing.Contains(id)) throw new KeyNotFoundException($"Runtime artifact '{id}' was not found.");
        try
        {
            artifact = resolver is DefaultRuntimeArtifactResolver defaultResolver
                ? await defaultResolver.ResolveUncachedAsync(id, cancellationToken)
                : await resolver.ResolveAsync(id.ToString(), cancellationToken);
            _artifacts.Add(id, artifact);
            return artifact;
        }
        catch (KeyNotFoundException)
        {
            _missing.Add(id);
            throw;
        }
    }
}
