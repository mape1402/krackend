using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;

/// <summary>Best-effort Redis fanout for runtime-monitor notifications.</summary>
public sealed class RedisRuntimeReactiveFanout : BackgroundService, IRuntimeReactiveFanout, IRuntimeStatisticsFanout, IRuntimeReactiveFanoutHealth
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly string _origin = Guid.NewGuid().ToString("N");
    private readonly RuntimeGossipOptions _options;
    private readonly IRedisRuntimeReactiveConnectionFactory _connectionFactory;
    private readonly ILogger<RedisRuntimeReactiveFanout> _logger;
    private IConnectionMultiplexer _connection;
    private ISubscriber _subscriber;
    private int _synchronized;

    public RedisRuntimeReactiveFanout(IOptions<RuntimeGossipOptions> options, IRedisRuntimeReactiveConnectionFactory connectionFactory, ILogger<RedisRuntimeReactiveFanout> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event Action<RuntimeReactiveEvent> Received;
    /// <inheritdoc />
    public event Action ResyncRequired;
    /// <inheritdoc />
    public event Action Updated;

    /// <inheritdoc />
    public bool IsSynchronized => _options.Enabled && Volatile.Read(ref _synchronized) == 1;

    /// <inheritdoc />
    public event Action<bool> SynchronizationChanged;

    /// <inheritdoc />
    public async Task PublishStatisticsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _subscriber is null || _connection?.IsConnected != true) return;
        await PublishMessage(new Message(_origin, "statistics", null), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null || !_options.Enabled || _subscriber is null || _connection?.IsConnected != true) return;
        await PublishMessage(new Message(_origin, "event", eventData), cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _connectionFactory.ConnectAsync(stoppingToken);
                _connection = connection;
                connection.ConnectionRestored += OnConnectionRestored;
                connection.ConnectionFailed += OnConnectionFailed;
                _subscriber = connection.GetSubscriber();
                await _subscriber.SubscribeAsync(RedisChannel.Literal(_options.ReactiveChannelName), OnMessage).WaitAsync(stoppingToken);
                UpdateSynchronization();
                RaiseResync();
                await BroadcastResync();
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Runtime notification fanout is unavailable; local notifications remain active."); }
            finally
            {
                _subscriber = null;
                SetSynchronized(false);
                var connection = _connection;
                _connection = null;
                if (connection is not null)
                {
                    connection.ConnectionRestored -= OnConnectionRestored;
                    connection.ConnectionFailed -= OnConnectionFailed;
                    connection.Dispose();
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task PublishMessage(Message message, CancellationToken cancellationToken)
    {
        var subscriber = _subscriber;
        if (subscriber is null) return;
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        try
        {
            await subscriber.PublishAsync(RedisChannel.Literal(_options.ReactiveChannelName), payload)
                .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            UpdateSynchronization();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            SetSynchronized(false);
            throw;
        }
    }

    private void OnConnectionRestored(object sender, ConnectionFailedEventArgs args)
    {
        UpdateSynchronization();
        RaiseResync();
        _ = BroadcastResync();
    }

    private void OnConnectionFailed(object sender, ConnectionFailedEventArgs args) => SetSynchronized(false);

    private void UpdateSynchronization() => SetSynchronized(_connection?.IsConnected == true &&
        _subscriber?.IsConnected(RedisChannel.Literal(_options.ReactiveChannelName)) == true);

    private void SetSynchronized(bool value)
    {
        if (!_options.Enabled || Interlocked.Exchange(ref _synchronized, value ? 1 : 0) == (value ? 1 : 0)) return;
        try { SynchronizationChanged?.Invoke(value); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not report runtime notification synchronization status."); }
    }

    private async Task BroadcastResync()
    {
        try { await PublishMessage(new Message(_origin, "resync", null), CancellationToken.None); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not request runtime monitor reconciliation after Redis recovered."); }
    }

    private void OnMessage(RedisChannel channel, RedisValue payload)
    {
        try
        {
            if (payload.IsNullOrEmpty) return;
            UpdateSynchronization();
            var message = JsonSerializer.Deserialize<Message>(payload.ToString(), SerializerOptions);
            if (message is null || message.Origin == _origin) return;
            if (message.Kind == "resync") RaiseResync();
            else if (message.Kind == "statistics") Updated?.Invoke();
            else if (message.Kind == "event" && message.Event is not null) Received?.Invoke(message.Event);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not process a runtime monitor notification."); }
    }

    private void RaiseResync()
    {
        try { ResyncRequired?.Invoke(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not request local runtime monitor reconciliation."); }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new IdConverter());
        return options;
    }

    private sealed record Message(string Origin, string Kind, RuntimeReactiveEvent Event);

    private sealed class IdConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(Ulid.Parse(reader.GetString()));

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}
