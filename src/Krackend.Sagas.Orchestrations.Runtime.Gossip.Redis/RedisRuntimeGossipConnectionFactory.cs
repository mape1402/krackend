using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>
/// Default Redis connection factory for runtime gossip.
/// </summary>
internal sealed class RedisRuntimeGossipConnectionFactory : IRedisRuntimeGossipConnectionFactory, IDisposable
{
    private readonly RuntimeGossipOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnectionMultiplexer _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRuntimeGossipConnectionFactory"/> class.
    /// </summary>
    public RedisRuntimeGossipConnectionFactory(IOptions<RuntimeGossipOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<IConnectionMultiplexer> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Runtime gossip is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
        {
            throw new InvalidOperationException("Runtime gossip Redis connection string is required.");
        }

        if (_connection is not null && _connection.IsConnected)
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null && _connection.IsConnected)
            {
                return _connection;
            }

            _connection?.Dispose();
            _connection = await ConnectionMultiplexer.ConnectAsync(_options.RedisConnectionString);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _gate.Dispose();
    }
}
