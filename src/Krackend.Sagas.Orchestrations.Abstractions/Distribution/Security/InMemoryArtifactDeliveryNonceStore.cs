using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// In-memory nonce store used by single-node hosts.
/// </summary>
public sealed class InMemoryArtifactDeliveryNonceStore : IArtifactDeliveryNonceStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonces = new();

    /// <inheritdoc />
    public Task<bool> TryStore(
        string keyId,
        string nonce,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        CleanupExpired(DateTimeOffset.UtcNow);
        var stored = _nonces.TryAdd($"{keyId}:{nonce}", expiresAt);
        return Task.FromResult(stored);
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        foreach (var item in _nonces)
        {
            if (item.Value <= now)
            {
                _nonces.TryRemove(item.Key, out _);
            }
        }
    }
}
