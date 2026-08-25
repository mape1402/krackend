using System.Text.Json;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>
/// Listens for artifact-ready gossip notifications through Redis pub/sub.
/// </summary>
public sealed class RedisRuntimeArtifactReadyGossipListener : IHostedService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisRuntimeGossipConnectionFactory _connectionFactory;
    private readonly RuntimeGossipOptions _options;
    private readonly ILogger<RedisRuntimeArtifactReadyGossipListener> _logger;
    private ISubscriber _subscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRuntimeArtifactReadyGossipListener"/> class.
    /// </summary>
    public RedisRuntimeArtifactReadyGossipListener(
        IServiceScopeFactory scopeFactory,
        IRedisRuntimeGossipConnectionFactory connectionFactory,
        IOptions<RuntimeGossipOptions> options,
        ILogger<RedisRuntimeArtifactReadyGossipListener> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var connection = await _connectionFactory.GetConnectionAsync(cancellationToken);
        _subscriber = connection.GetSubscriber();
        await _subscriber.SubscribeAsync(
            RedisChannel.Literal(_options.ChannelName),
            (channel, payload) =>
            {
                _ = Task.Run(() => HandleMessageAsync(payload));
            });
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is null || !_options.Enabled)
        {
            return;
        }

        await _subscriber.UnsubscribeAsync(RedisChannel.Literal(_options.ChannelName));
    }

    private async Task HandleMessageAsync(RedisValue payload)
    {
        try
        {
            if (payload.IsNullOrEmpty)
            {
                return;
            }

            var message = JsonSerializer.Deserialize<RuntimeArtifactReadyGossipMessage>(
                payload.ToString(),
                SerializerOptions);
            if (message is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactReadyGossipHandler>();
            await handler.HandleAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Runtime artifact-ready gossip handling failed.");
        }
    }
}
