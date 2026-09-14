using System.Text.Json;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>
/// Publishes artifact-ready gossip notifications through Redis pub/sub.
/// </summary>
public sealed class RedisRuntimeArtifactReadyGossipPublisher : IRuntimeArtifactReadyGossipPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IRedisRuntimeGossipConnectionFactory _connectionFactory;
    private readonly RuntimeGossipOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRuntimeArtifactReadyGossipPublisher"/> class.
    /// </summary>
    public RedisRuntimeArtifactReadyGossipPublisher(
        IRedisRuntimeGossipConnectionFactory connectionFactory,
        IOptions<RuntimeGossipOptions> options)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        RuntimeArtifactReadyGossipMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            return;
        }

        var connection = await _connectionFactory.GetConnectionAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        await connection.GetSubscriber().PublishAsync(RedisChannel.Literal(_options.ChannelName), payload);
    }
}
