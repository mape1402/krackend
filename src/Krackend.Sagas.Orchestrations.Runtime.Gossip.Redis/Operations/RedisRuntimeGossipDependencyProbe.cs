using Krackend.Sagas.Orchestrations.Runtime.Operations;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis.Operations;

/// <summary>
/// Checks Redis gossip availability as an optional runtime dependency.
/// </summary>
internal sealed class RedisRuntimeGossipDependencyProbe : IRuntimeDependencyProbe
{
    private readonly IRedisRuntimeGossipConnectionFactory _connectionFactory;

    public RedisRuntimeGossipDependencyProbe(IRedisRuntimeGossipConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public string Name => "redis-gossip";

    public RuntimeDependencyKind Kind => RuntimeDependencyKind.Optional;

    public async ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionFactory.GetConnectionAsync(cancellationToken);
            return new RuntimeDependencyProbeResult(
                Name,
                Kind,
                connection.IsConnected,
                connection.IsConnected ? null : "Redis gossip connection is not connected.");
        }
        catch (RedisConnectionException exception)
        {
            return new RuntimeDependencyProbeResult(Name, Kind, false, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return new RuntimeDependencyProbeResult(Name, Kind, false, exception.Message);
        }
    }
}
