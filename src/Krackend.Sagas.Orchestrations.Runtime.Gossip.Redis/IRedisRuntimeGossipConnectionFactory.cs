using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>
/// Provides Redis connections used by runtime gossip.
/// </summary>
public interface IRedisRuntimeGossipConnectionFactory
{
    /// <summary>
    /// Gets a Redis connection multiplexer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redis connection multiplexer.</returns>
    Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default);
}
