namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using StackExchange.Redis;

public sealed class RedisRuntimeReactiveFanoutRealInfrastructureTests
{
    [Fact(Timeout = 180_000)]
    public async Task ReplicasExchangeNotificationsAndRequestResyncAfterRedisRestarts()
    {
        var channel = $"runtime-test-{Guid.NewGuid():N}";
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await using var redis = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(port, 6379)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();
        await redis.StartAsync();
        var options = Options.Create(new RuntimeGossipOptions
        {
            Enabled = true,
            RedisConnectionString = $"localhost:{port}",
            ReactiveChannelName = channel
        });
        var firstFactory = new ObservedRedisConnectionFactory(options.Value.RedisConnectionString);
        var secondFactory = new ObservedRedisConnectionFactory(options.Value.RedisConnectionString);
        using var first = new RedisRuntimeReactiveFanout(options, firstFactory, NullLogger<RedisRuntimeReactiveFanout>.Instance);
        using var second = new RedisRuntimeReactiveFanout(options, secondFactory, NullLogger<RedisRuntimeReactiveFanout>.Instance);
        var firstResyncs = 0;
        var secondResyncs = 0;
        first.ResyncRequired += () => Interlocked.Increment(ref firstResyncs);
        second.ResyncRequired += () => Interlocked.Increment(ref secondResyncs);
        var received = new System.Collections.Concurrent.ConcurrentQueue<RuntimeReactiveEvent>();
        second.Received += received.Enqueue;

        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);
        try
        {
            await PollingAssert.EventuallyAsync(() => Task.FromResult(Volatile.Read(ref firstResyncs) > 0 && Volatile.Read(ref secondResyncs) > 0 &&
                    first.IsSynchronized && second.IsSynchronized),
                ready => ready, "Both replicas must subscribe before publishing");
            Assert.True(first.IsSynchronized);
            Assert.True(second.IsSynchronized);
            var before = CreateEvent();
            await first.PublishAsync(before);
            await PollingAssert.EventuallyAsync(() => Task.FromResult(received.Any(item => item.Id == before.Id)),
                delivered => delivered, "The second replica did not receive the first notification");

            await redis.StopAsync();
            await PollingAssert.EventuallyAsync(() => Task.FromResult(firstFactory.Connection?.IsConnected == false && secondFactory.Connection?.IsConnected == false),
                disconnected => disconnected, "Both Redis connections must detect the outage");
            Assert.False(first.IsSynchronized);
            Assert.False(second.IsSynchronized);
            var firstBeforeRecovery = Volatile.Read(ref firstResyncs);
            var secondBeforeRecovery = Volatile.Read(ref secondResyncs);
            var missed = CreateEvent();
            await first.PublishAsync(missed);
            await redis.StartAsync();
            await PollingAssert.EventuallyAsync(() => Task.FromResult(Volatile.Read(ref firstResyncs) > firstBeforeRecovery && Volatile.Read(ref secondResyncs) > secondBeforeRecovery &&
                    first.IsSynchronized && second.IsSynchronized),
                recovered => recovered, "Both replicas must request reconciliation when Redis recovers", TimeSpan.FromSeconds(90));
            Assert.True(first.IsSynchronized);
            Assert.True(second.IsSynchronized);
            Assert.DoesNotContain(received, item => item.Id == missed.Id);

            var after = CreateEvent();
            await first.PublishAsync(after);
            await PollingAssert.EventuallyAsync(() => Task.FromResult(received.Any(item => item.Id == after.Id)),
                delivered => delivered, "Notifications must resume after reconciliation");
        }
        finally
        {
            await first.StopAsync(CancellationToken.None);
            await second.StopAsync(CancellationToken.None);
        }
    }

    private static RuntimeReactiveEvent CreateEvent() => new()
    {
        Id = new Id(Ulid.NewUlid()),
        OrchestrationInstanceId = new Id(Ulid.NewUlid()),
        EventName = "transition",
        TransitionType = "changed",
        OrchestrationDefinitionKey = "sales",
        CorrelationId = "correlation",
        SagaId = "saga",
        ExecutionKey = "execution",
        InstanceStatus = "Running",
        ProducedBy = "test"
    };

    private sealed class ObservedRedisConnectionFactory(string connectionString) : IRedisRuntimeReactiveConnectionFactory
    {
        public IConnectionMultiplexer? Connection { get; private set; }

        public async Task<IConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken = default)
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.KeepAlive = 1;
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 2000;
            Connection = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(cancellationToken);
            return Connection;
        }
    }
}
