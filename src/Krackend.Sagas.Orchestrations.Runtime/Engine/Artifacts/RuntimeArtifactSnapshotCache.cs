using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

/// <summary>
/// Process-wide immutable artifact snapshot cache. Runtime lifecycle operations
/// explicitly invalidate entries, while concurrent activations share the same
/// initial load instead of stampeding primary storage.
/// </summary>
public sealed class RuntimeArtifactSnapshotCache
{
    private readonly ConcurrentDictionary<Id, Lazy<Task<ResolvedOrchestrationArtifact>>> _entries = new();

    internal async Task<ResolvedOrchestrationArtifact> GetOrAddAsync(
        Id artifactId,
        Func<CancellationToken, Task<ResolvedOrchestrationArtifact>> loader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var candidate = new Lazy<Task<ResolvedOrchestrationArtifact>>(
            () => loader(CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var entry = _entries.GetOrAdd(artifactId, candidate);
        try
        {
            return await entry.Value.WaitAsync(cancellationToken);
        }
        catch
        {
            if (entry.IsValueCreated && (entry.Value.IsFaulted || entry.Value.IsCanceled))
                _entries.TryRemove(new KeyValuePair<Id, Lazy<Task<ResolvedOrchestrationArtifact>>>(artifactId, entry));
            throw;
        }
    }

    internal void Invalidate(Id artifactId) => _entries.TryRemove(artifactId, out _);

    internal void Invalidate(string artifactId)
    {
        if (Ulid.TryParse(artifactId, out var parsed)) Invalidate(new Id(parsed));
    }
}
