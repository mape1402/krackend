using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

internal sealed class RedisRuntimeReactiveConnectionFactory(IOptions<RuntimeGossipOptions> options)
    : IRedisRuntimeReactiveConnectionFactory
{
    public async Task<IConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var configuration = ConfigurationOptions.Parse(options.Value.RedisConnectionString);
        configuration.AbortOnConnectFail = false;
        var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        if (cancellationToken.IsCancellationRequested)
        {
            connection.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return connection;
    }
}
