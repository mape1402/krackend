using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>Creates an independent Redis connection for runtime notification fanout.</summary>
public interface IRedisRuntimeReactiveConnectionFactory
{
    /// <summary>Connects to Redis without requiring it to be available at startup.</summary>
    Task<IConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken = default);
}
