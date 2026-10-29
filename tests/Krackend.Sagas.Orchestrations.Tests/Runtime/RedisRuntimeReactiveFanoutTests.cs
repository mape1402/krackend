using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RedisRuntimeReactiveFanoutTests
{
    [Fact]
    public async Task DisabledFanoutDoesNotConnect()
    {
        var factory = Substitute.For<IRedisRuntimeReactiveConnectionFactory>();
        using var fanout = Create(factory, false);
        await fanout.StartAsync(CancellationToken.None);
        Assert.False(fanout.IsSynchronized);
        await fanout.PublishAsync(CreateEvent());
        await fanout.PublishStatisticsUpdatedAsync();
        await fanout.StopAsync(CancellationToken.None);
        await factory.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StatisticsUpdatesArePublishedAndForwardedToOtherReplicas()
    {
        var factory = Substitute.For<IRedisRuntimeReactiveConnectionFactory>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        var subscribed = new TaskCompletionSource<Action<RedisChannel, RedisValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        RedisValue payload = RedisValue.Null;
        factory.ConnectAsync(Arg.Any<CancellationToken>()).Returns(connection);
        connection.IsConnected.Returns(true);
        connection.GetSubscriber(Arg.Any<object>()).Returns(subscriber);
        subscriber.SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Do<Action<RedisChannel, RedisValue>>(handler => subscribed.TrySetResult(handler)), Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);
        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Do<RedisValue>(value => payload = value), Arg.Any<CommandFlags>()).Returns(1L);
        using var sender = Create(factory, true);
        using var receiver = Create(factory, true);
        await sender.StartAsync(CancellationToken.None);
        await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sender.PublishStatisticsUpdatedAsync();
        var statisticsPayload = payload;
        var receiverSubscription = new TaskCompletionSource<Action<RedisChannel, RedisValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Do<Action<RedisChannel, RedisValue>>(handler => receiverSubscription.TrySetResult(handler)), Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);
        var updated = 0;
        receiver.Updated += () => updated++;
        await receiver.StartAsync(CancellationToken.None);
        var receiverHandler = await receiverSubscription.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receiverHandler(RedisChannel.Literal("events"), statisticsPayload);
        Assert.Equal(1, updated);
        await sender.StopAsync(CancellationToken.None);
        await receiver.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublishFailureMarksFanoutDegradedAndSuccessfulPublishRestoresIt()
    {
        var factory = Substitute.For<IRedisRuntimeReactiveConnectionFactory>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.ConnectAsync(Arg.Any<CancellationToken>()).Returns(connection);
        connection.IsConnected.Returns(true);
        connection.GetSubscriber(Arg.Any<object>()).Returns(subscriber);
        subscriber.IsConnected(Arg.Any<RedisChannel>()).Returns(true);
        subscriber.SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);
        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>()).Returns(1L);
        using var fanout = Create(factory, true);
        fanout.SynchronizationChanged += synchronized => { if (synchronized) ready.TrySetResult(); };
        await fanout.StartAsync(CancellationToken.None);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(_ => Task.FromException<long>(new TimeoutException()));
        await Assert.ThrowsAsync<TimeoutException>(() => fanout.PublishAsync(CreateEvent()));
        Assert.False(fanout.IsSynchronized);

        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>()).Returns(1L);
        await fanout.PublishAsync(CreateEvent());
        Assert.True(fanout.IsSynchronized);
        await fanout.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RemoteNotificationsAreForwardedAndOwnMessagesAreIgnored()
    {
        var factory = Substitute.For<IRedisRuntimeReactiveConnectionFactory>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        var subscribed = new TaskCompletionSource<Action<RedisChannel, RedisValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        RedisValue payload = RedisValue.Null;
        factory.ConnectAsync(Arg.Any<CancellationToken>()).Returns(connection);
        connection.IsConnected.Returns(true);
        connection.GetSubscriber(Arg.Any<object>()).Returns(subscriber);
        subscriber.SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Do<Action<RedisChannel, RedisValue>>(handler => subscribed.TrySetResult(handler)), Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);
        subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Do<RedisValue>(value => payload = value), Arg.Any<CommandFlags>()).Returns(1L);

        using var sender = Create(factory, true);
        using var receiver = Create(factory, true);
        var received = new TaskCompletionSource<RuntimeReactiveEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        sender.Received += _ => count++;
        receiver.Received += value => received.TrySetResult(value);
        await sender.StartAsync(CancellationToken.None);
        var senderHandler = await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sender.PublishAsync(CreateEvent());
        var eventPayload = payload;
        senderHandler(RedisChannel.Literal("events"), eventPayload);
        Assert.Equal(0, count);

        var receiverSubscription = new TaskCompletionSource<Action<RedisChannel, RedisValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.SubscribeAsync(Arg.Any<RedisChannel>(), Arg.Do<Action<RedisChannel, RedisValue>>(handler => receiverSubscription.TrySetResult(handler)), Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);
        await receiver.StartAsync(CancellationToken.None);
        var receiverHandler = await receiverSubscription.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receiverHandler(RedisChannel.Literal("events"), RedisValue.Null);
        receiverHandler(RedisChannel.Literal("events"), "not-json");
        receiverHandler(RedisChannel.Literal("events"), eventPayload);
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("sales", result.OrchestrationDefinitionKey);
        Assert.Equal("correlation", result.CorrelationId);
        await sender.StopAsync(CancellationToken.None);
        await receiver.StopAsync(CancellationToken.None);
    }

    private static RedisRuntimeReactiveFanout Create(IRedisRuntimeReactiveConnectionFactory factory, bool enabled) =>
        new(Options.Create(new RuntimeGossipOptions { Enabled = enabled, ReactiveChannelName = "events" }), factory, NullLogger<RedisRuntimeReactiveFanout>.Instance);

    private static RuntimeReactiveEvent CreateEvent() => new()
    {
        Id = new Id(Ulid.NewUlid()),
        Revision = "1",
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
}
