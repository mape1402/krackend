using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Topology;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

/// <summary>
/// Accepts broker deliveries directly into independent asynchronous dispatches.
/// There is deliberately no process-wide channel, reader, worker pool, semaphore,
/// or concurrency ceiling between the broker and an orchestration activation.
/// </summary>
internal sealed class DirectParallelConsumingManager : IConsumingManager
{
    private readonly IConsumingDispatcher _dispatcher;
    private readonly IReadOnlyCollection<IMessageBrokerConsumingAdapter> _adapters;
    private readonly IConsumingConfigurator _configurator;
    private readonly ITopologyProvisioningService _topology;
    private readonly GlobalSettings _settings;
    private readonly ILogger<DirectParallelConsumingManager> _logger;
    private readonly Func<string, RawPayload> _rawPayloadFactory;
    private readonly ConcurrentDictionary<long, Task> _inFlight = new();
    private long _sequence;
    private int _accepting;

    public DirectParallelConsumingManager(
        IConsumingDispatcher dispatcher,
        IEnumerable<IMessageBrokerConsumingAdapter> adapters,
        IConsumingConfigurator configurator,
        ITopologyProvisioningService topology,
        IOptions<GlobalSettings> settings,
        ILogger<DirectParallelConsumingManager> logger,
        IServiceProvider services)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _adapters = adapters?.ToArray() ?? throw new ArgumentNullException(nameof(adapters));
        _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rawPayloadFactory = ResolveRawPayloadFactory(
            services ?? throw new ArgumentNullException(nameof(services)));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in _configurator.GetAllEndpoints())
            await _topology.EnsureConsumeTopologyAsync(endpoint, cancellationToken);

        Volatile.Write(ref _accepting, 1);
        try
        {
            foreach (var adapter in _adapters)
            {
                adapter.MessageConsumed += OnMessageConsumed;
                adapter.MessageConsumedAsync += OnMessageConsumedAsync;
                await adapter.StartConsumeAsync(cancellationToken);
            }
        }
        catch
        {
            Volatile.Write(ref _accepting, 0);
            DetachHandlers();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Volatile.Write(ref _accepting, 0);
        DetachHandlers();

        foreach (var adapter in _adapters)
        {
            try
            {
                await adapter.StopConsumeAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception,
                    "Failed to stop adapter {Adapter} gracefully.", adapter.GetType().Name);
            }
        }

        var remaining = _inFlight.Values.ToArray();
        if (remaining.Length > 0)
            await Task.WhenAll(remaining).WaitAsync(cancellationToken);
    }

    private ValueTask OnMessageConsumedAsync(
        object sender,
        MessageConsumedEventArgs message,
        CancellationToken cancellationToken)
    {
        Track(message, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private void OnMessageConsumed(object sender, MessageConsumedEventArgs message)
        => Track(message, CancellationToken.None);

    private void Track(MessageConsumedEventArgs message, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _accepting) == 0)
            return;

        var id = Interlocked.Increment(ref _sequence);

        // Never execute the handler's synchronous prefix on Rabbit's delivery loop.
        // Warm SqlClient and EF operations can complete several awaits synchronously;
        // invoking the pipeline inline therefore turns one broker callback into an
        // accidental serial executor. Give every delivery an independent .NET work
        // item. The runtime does not impose a worker count, semaphore, or concurrency
        // ceiling; the process-wide adaptive scheduler owns parallelism.
        var dispatch = Task.Run(
            () => DispatchAsync(message, cancellationToken),
            CancellationToken.None);
        _inFlight[id] = dispatch;
        if (dispatch.IsCompleted)
        {
            _inFlight.TryRemove(id, out _);
            return;
        }

        _ = dispatch.ContinueWith(
            (_, state) =>
            {
                var tracked = ((DirectParallelConsumingManager Owner, long Id))state!;
                tracked.Owner._inFlight.TryRemove(tracked.Id, out _);
            },
            (this, id),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task DispatchAsync(
        MessageConsumedEventArgs message,
        CancellationToken deliveryCancellation)
    {
        var execution = _settings.ConsumerExecution;
        var timeout = execution?.HandlerTimeout > TimeSpan.Zero
            ? execution.HandlerTimeout
            : TimeSpan.FromSeconds(30);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = deliveryCancellation.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(
                timeoutSource.Token, deliveryCancellation)
            : CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token);

        try
        {
            var rawPayload = _rawPayloadFactory(message.RawPayload);
            var topic = string.IsNullOrWhiteSpace(_settings.Domain)
                ? message.Topic
                : message.Topic.Replace(_settings.Domain + ".", string.Empty,
                    StringComparison.Ordinal);
            var subscription = string.Equals(message.Subscription, "Default",
                StringComparison.Ordinal) ? "Default" : message.Subscription;

            await _dispatcher.DispatchAsync(
                topic,
                subscription,
                rawPayload,
                message.CompleteAsync,
                message.RetryAsync,
                linkedSource.Token);

            if (execution?.AcknowledgementMode == MessageAcknowledgementMode.OnHandlerSuccess)
                await message.CompleteAsync(CancellationToken.None);
        }
        catch (ConsumeObserverRetryException exception)
        {
            // The observer already attempted transport redelivery and that request
            // failed. Keep the delivery unsettled so broker recovery remains possible.
            _logger.LogError(exception,
                "A delivery could not request observer redelivery; it remains unsettled.");
        }
        catch (OperationCanceledException exception)
            when (!deliveryCancellation.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            await RetryAfterFailureAsync(message,
                new TimeoutException($"Consumer handler exceeded {timeout}.", exception));
        }
        catch (OperationCanceledException) when (deliveryCancellation.IsCancellationRequested)
        {
            // The broker owns the unsettled delivery when its delivery token is cancelled.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "An unexpected error occurred while consuming topic {Topic}.", message.Topic);
            if (execution?.AcknowledgementMode == MessageAcknowledgementMode.OnHandlerSuccess)
                await RetryAfterFailureAsync(message, exception);
        }
    }

    private async Task RetryAfterFailureAsync(
        MessageConsumedEventArgs message,
        Exception exception)
    {
        try
        {
            await message.RetryAsync(exception, CancellationToken.None);
        }
        catch (Exception settlementException)
        {
            _logger.LogError(settlementException,
                "Could not request redelivery for topic {Topic}.", message.Topic);
        }
    }

    private void DetachHandlers()
    {
        foreach (var adapter in _adapters)
        {
            adapter.MessageConsumed -= OnMessageConsumed;
            adapter.MessageConsumedAsync -= OnMessageConsumedAsync;
        }
    }

    private static Func<string, RawPayload> ResolveRawPayloadFactory(
        IServiceProvider services)
    {
        // Pigeon keeps this contract internal even though it is the only parser that
        // honors the configured wrapped-payload naming policy. Resolve it once and
        // bind a strongly typed delegate; there is no reflection on the message path.
        var contract = typeof(RawPayload).Assembly.GetType(
            "Pigeon.Messaging.Consuming.IRawPayloadFactory",
            throwOnError: true)!;
        var factory = services.GetRequiredService(contract);
        var create = contract.GetMethod("Create", [typeof(string)])
            ?? throw new MissingMethodException(contract.FullName, "Create");
        return (Func<string, RawPayload>)create.CreateDelegate(
            typeof(Func<string, RawPayload>), factory);
    }
}
