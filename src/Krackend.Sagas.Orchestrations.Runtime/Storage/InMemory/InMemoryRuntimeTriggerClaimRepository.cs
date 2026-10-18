namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Thread-safe in-memory implementation used by local hosts and tests.
/// </summary>
internal sealed class InMemoryRuntimeTriggerClaimRepository : IRuntimeTriggerClaimRepository
{
    private readonly InMemoryRuntimeStore _store;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public InMemoryRuntimeTriggerClaimRepository(InMemoryRuntimeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task<RuntimeTriggerClaimResult> ClaimAsync(
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds,
        CancellationToken cancellationToken = default)
    {
        var keys = Normalize(identities);
        if (keys.Length == 0)
        {
            return Task.FromResult(RuntimeTriggerClaimResult.Acquired);
        }

        var replaceable = (replaceableArtifactIds ?? Array.Empty<Id>()).ToHashSet();
        lock (_store.TriggerClaimsGate)
        {
            foreach (var key in keys)
            {
                if (_store.TriggerClaims.TryGetValue(key, out var existing) &&
                    existing.RuntimeOrchestrationArtifactId != artifactId &&
                    !replaceable.Contains(existing.RuntimeOrchestrationArtifactId))
                {
                    return Task.FromResult(RuntimeTriggerClaimResult.Conflict(existing.RuntimeOrchestrationArtifactId));
                }
            }

            var now = DateTime.UtcNow;
            foreach (var key in keys)
            {
                _store.TriggerClaims[key] = new RuntimeTriggerClaim
                {
                    Id = key,
                    RuntimeOrchestrationArtifactId = artifactId,
                    UpdatedOnUtc = now
                };
            }
        }

        return Task.FromResult(RuntimeTriggerClaimResult.Acquired);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
    {
        var ids = (artifactIds ?? Array.Empty<Id>()).ToHashSet();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        lock (_store.TriggerClaimsGate)
        {
            foreach (var item in _store.TriggerClaims.Where(x => ids.Contains(x.Value.RuntimeOrchestrationArtifactId)).ToArray())
            {
                _store.TriggerClaims.TryRemove(item.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    private static string[] Normalize(IReadOnlyCollection<string> identities)
        => (identities ?? Array.Empty<string>())
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => identity.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
