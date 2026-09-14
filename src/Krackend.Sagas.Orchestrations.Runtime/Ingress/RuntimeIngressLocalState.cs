using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// In-memory local state for ingress generations applied by one runtime process.
/// </summary>
internal sealed class RuntimeIngressLocalState : IRuntimeIngressLocalState
{
    private readonly ConcurrentDictionary<string, long> _generations = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsApplied(string artifactId, long ingressGeneration)
        => _generations.TryGetValue(artifactId, out var currentGeneration) &&
            currentGeneration >= ingressGeneration;

    /// <inheritdoc />
    public void MarkApplied(string artifactId, long ingressGeneration)
        => _generations.AddOrUpdate(
            artifactId,
            ingressGeneration,
            (_, currentGeneration) => Math.Max(currentGeneration, ingressGeneration));

    /// <inheritdoc />
    public void Forget(string artifactId)
        => _generations.TryRemove(artifactId, out _);

    /// <inheritdoc />
    public void Clear()
        => _generations.Clear();
}
