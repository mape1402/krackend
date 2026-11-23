using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;

/// <summary>
/// Shares the immutable messaging backchannel projected for one runtime artifact.
/// Lifecycle projection and gossip invalidate entries when an artifact changes.
/// </summary>
public sealed class RuntimeBackchannelReplyAddressCache
{
    private readonly ConcurrentDictionary<Id, Lazy<Task<OrchestrationReplyAddress>>> _entries = new();

    internal async Task<OrchestrationReplyAddress> GetOrAddAsync(
        Id artifactId,
        Func<CancellationToken, Task<OrchestrationReplyAddress>> loader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var candidate = new Lazy<Task<OrchestrationReplyAddress>>(
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
                _entries.TryRemove(new KeyValuePair<Id, Lazy<Task<OrchestrationReplyAddress>>>(artifactId, entry));
            throw;
        }
    }

    internal void Invalidate(Id artifactId) => _entries.TryRemove(artifactId, out _);

    internal void Invalidate(string artifactId)
    {
        if (Ulid.TryParse(artifactId, out var parsed)) Invalidate(new Id(parsed));
    }
}
