namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Reflection;
using System.Text.Json.Nodes;

public sealed class RuntimeReactiveEventMessageTests
{
    [Fact]
    public void FromMapsRuntimeReactiveEventToSignalRPayload()
    {
        var eventId = Id.New();
        var instanceId = Id.New();
        var stageExecutionId = Id.New();
        var taskExecutionId = Id.New();
        var attemptId = Id.New();
        var occurredOnUtc = DateTime.UtcNow;
        var payload = JsonNode.Parse("""{"task":"inventories.reserve"}""");
        var eventData = new RuntimeReactiveEvent
        {
            Id = eventId,
            EventName = "runtime.transition.created",
            TransitionType = "TaskCallbackCompleted",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.2.0",
            OrchestrationInstanceId = instanceId,
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            StageExecutionId = stageExecutionId,
            StageKey = "inventory",
            TaskExecutionId = taskExecutionId,
            TaskKey = "inventories.reserve",
            TaskExecutionAttemptId = attemptId,
            FromStatus = "WaitingResponse",
            ToStatus = "Completed",
            InstanceStatus = "Running",
            OccurredOnUtc = occurredOnUtc,
            Message = "Task completed.",
            Payload = payload,
            ProducedBy = "tests"
        };

        var message = RuntimeReactiveEventMessage.From(eventData);

        Assert.Equal(eventId.ToString(), message.Id);
        Assert.Equal("runtime.transition.created", message.EventName);
        Assert.Equal("TaskCallbackCompleted", message.TransitionType);
        Assert.Equal("sales.sale.created", message.OrchestrationDefinitionKey);
        Assert.Equal("1.2.0", message.OrchestrationVersion);
        Assert.Equal(instanceId.ToString(), message.OrchestrationInstanceId);
        Assert.Equal("correlation-1", message.CorrelationId);
        Assert.Equal("saga-1", message.SagaId);
        Assert.Equal(stageExecutionId.ToString(), message.StageExecutionId);
        Assert.Equal("inventory", message.StageKey);
        Assert.Equal(taskExecutionId.ToString(), message.TaskExecutionId);
        Assert.Equal("inventories.reserve", message.TaskKey);
        Assert.Equal(attemptId.ToString(), message.TaskExecutionAttemptId);
        Assert.Equal("WaitingResponse", message.FromStatus);
        Assert.Equal("Completed", message.ToStatus);
        Assert.Equal("Running", message.InstanceStatus);
        Assert.Equal(occurredOnUtc, message.OccurredOnUtc);
        Assert.Same(payload, message.Payload);
        Assert.Equal("tests", message.ProducedBy);
    }

    [Fact]
    public void FromMapsOptionalRuntimeReactiveEventIdsAsNull()
    {
        var eventData = new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = "runtime.instance.created",
            TransitionType = "InstanceCreated",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            InstanceStatus = "Running",
            ProducedBy = "tests"
        };

        var message = RuntimeReactiveEventMessage.From(eventData);

        Assert.Null(message.StageExecutionId);
        Assert.Null(message.TaskExecutionId);
        Assert.Null(message.TaskExecutionAttemptId);
    }

    [Fact]
    public async Task PublisherQueuesEventsAndIgnoresNullPayloads()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var publisher = new SignalRRuntimeReactiveEventPublisher(queue);
        var eventData = new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = "runtime.transition.created",
            TransitionType = "StageStarted",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            InstanceStatus = "Running",
            ProducedBy = "tests"
        };

        Assert.False(queue.TryEnqueue(null!));
        await publisher.Publish(null!, CancellationToken.None);
        await publisher.Publish(eventData, CancellationToken.None);

        Assert.True(queue.Reader.TryRead(out var message));
        Assert.Equal("StageStarted", message.TransitionType);
        Assert.Throws<ArgumentNullException>(() => new SignalRRuntimeReactiveEventPublisher(null!));
    }

    [Fact]
    public async Task FanoutFailureDoesNotPreventLocalDelivery()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var fanout = new FakeFanout { FailPublish = true };
        var publisher = new SignalRRuntimeReactiveEventPublisher(queue, fanout, NullLogger<SignalRRuntimeReactiveEventPublisher>.Instance);
        var eventData = CreateEvent();
        await publisher.Publish(eventData);
        Assert.Same(eventData, fanout.LastPublished);
        Assert.True(queue.Reader.TryRead(out var message));
        Assert.Equal(eventData.Id.ToString(), message.Id);
    }

    [Fact]
    public async Task RemoteEventsReachLocalQueueAndReconnectionRequestsResync()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var hub = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.Group("runtime:node").Returns(proxy);
        proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var fanout = new FakeFanout();
        var dispatcher = new SignalRRuntimeReactiveEventDispatcher(queue, hub, NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance, fanout);
        var eventData = CreateEvent();

        fanout.Deliver(eventData);
        Assert.True(queue.Reader.TryRead(out var message));
        Assert.Equal(eventData.Id.ToString(), message.Id);
        fanout.Resync();
        await proxy.Received(1).SendCoreAsync("runtime.resync", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        dispatcher.Dispose();
        fanout.Deliver(eventData);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task DispatcherSendsRuntimeEventsToRuntimeAndInstanceGroups()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var hubContext = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        proxy.SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var dispatcher = new SignalRRuntimeReactiveEventDispatcher(
            queue,
            hubContext,
            NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance);
        var message = RuntimeReactiveEventMessage.From(new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = "runtime.transition.created",
            TransitionType = "TaskCompleted",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            InstanceStatus = "Running",
            ProducedBy = "tests"
        });

        await InvokeDispatchAsync(dispatcher, message, CancellationToken.None);

        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(groups =>
            groups.Contains("runtime:node") &&
            groups.Contains($"runtime:instance:{message.OrchestrationInstanceId}")));
        await proxy.Received(1).SendCoreAsync(
            "runtime.transition",
            Arg.Is<object?[]>(args => ReferenceEquals(args.Single(), message)),
            Arg.Any<CancellationToken>());

        Assert.Throws<ArgumentNullException>(() => new SignalRRuntimeReactiveEventDispatcher(null!, hubContext, NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance));
        Assert.Throws<ArgumentNullException>(() => new SignalRRuntimeReactiveEventDispatcher(queue, null!, NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance));
        Assert.Throws<ArgumentNullException>(() => new SignalRRuntimeReactiveEventDispatcher(queue, hubContext, null!));
    }

    [Fact]
    public async Task DispatcherBackgroundLoopSendsQueuedRuntimeEvents()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var hubContext = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hubContext.Clients.Returns(clients);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        proxy.SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                sent.TrySetResult();
                return Task.CompletedTask;
            });
        var dispatcher = new SignalRRuntimeReactiveEventDispatcher(
            queue,
            hubContext,
            NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance);
        var message = RuntimeReactiveEventMessage.From(new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = "runtime.transition.created",
            TransitionType = "TaskCompleted",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            InstanceStatus = "Running",
            ProducedBy = "tests"
        });

        await dispatcher.StartAsync(CancellationToken.None);
        queue.TryEnqueue(message);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await dispatcher.StopAsync(CancellationToken.None);

        await proxy.Received(1).SendCoreAsync(
            "runtime.transition",
            Arg.Is<object?[]>(args => ReferenceEquals(args.Single(), message)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatcherSwallowsSignalRDispatchFailures()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var hubContext = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        proxy.SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("connection closed"));
        var dispatcher = new SignalRRuntimeReactiveEventDispatcher(
            queue,
            hubContext,
            NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance);
        var message = RuntimeReactiveEventMessage.From(new RuntimeReactiveEvent
        {
            Id = Id.New(),
            EventName = "runtime.transition.created",
            TransitionType = "TaskFailed",
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created",
            InstanceStatus = "Running",
            ProducedBy = "tests"
        });

        await InvokeDispatchAsync(dispatcher, message, CancellationToken.None);

        await proxy.Received(1).SendCoreAsync(
            "runtime.transition",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RuntimeReactiveHubSubscribesRuntimeAndInstanceGroups()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("connection-1");
        var groups = Substitute.For<IGroupManager>();
        groups.AddToGroupAsync("connection-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var hub = new RuntimeReactiveHub
        {
            Context = context,
            Groups = groups
        };

        await hub.WatchRuntime();
        await hub.WatchInstance("instance-1");
        await hub.WatchInstance(" ");

        await groups.Received(1).AddToGroupAsync("connection-1", "runtime:node", Arg.Any<CancellationToken>());
        await groups.Received(1).AddToGroupAsync("connection-1", "runtime:instance:instance-1", Arg.Any<CancellationToken>());
        await groups.DidNotReceive().AddToGroupAsync("connection-1", "runtime:instance: ", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RuntimeSubscriptionReportsReplicaSynchronization(bool synchronized)
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("connection-1");
        var groups = Substitute.For<IGroupManager>();
        groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var fanout = new FakeHealthFanout { IsSynchronized = synchronized };
        var hub = new RuntimeReactiveHub(fanout) { Context = context, Groups = groups };

        Assert.Equal(synchronized, await hub.WatchRuntime());
    }

    [Fact]
    public async Task DispatcherReportsSynchronizationChangesAndUnsubscribesOnDispose()
    {
        var hub = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.Group("runtime:node").Returns(proxy);
        proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var fanout = new FakeHealthFanout();
        var dispatcher = new SignalRRuntimeReactiveEventDispatcher(new SignalRRuntimeReactiveEventQueue(), hub,
            NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance, fanout);

        fanout.ChangeSynchronization(false);
        fanout.ChangeSynchronization(true);
        await proxy.Received(1).SendCoreAsync("runtime.fanout.health", Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], false)), Arg.Any<CancellationToken>());
        await proxy.Received(1).SendCoreAsync("runtime.fanout.health", Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], true)), Arg.Any<CancellationToken>());
        dispatcher.Dispose();
        fanout.ChangeSynchronization(false);
        await proxy.Received(1).SendCoreAsync("runtime.fanout.health", Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], false)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RuntimeReactiveEndpointMapsConfiguredHubRoute()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        builder.Services.Configure<OrchestratorRuntimeWebUIOptions>(options => options.RoutePrefix = "/runtime-ui/");
        using var app = builder.Build();

        var returned = app.MapOrchestratorRuntimeReactiveHub();

        Assert.Same(app, returned);
    }

    private static Task InvokeDispatchAsync(
        SignalRRuntimeReactiveEventDispatcher dispatcher,
        RuntimeReactiveEventMessage message,
        CancellationToken cancellationToken)
    {
        var method = typeof(SignalRRuntimeReactiveEventDispatcher).GetMethod(
            "Dispatch",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(dispatcher, [message, cancellationToken])!;
    }

    private static RuntimeReactiveEvent CreateEvent() => new()
    {
        Id = Id.New(), EventName = "runtime.transition.created", TransitionType = "TaskCompleted",
        OrchestrationDefinitionKey = "sales", OrchestrationInstanceId = Id.New(), CorrelationId = "correlation",
        SagaId = "saga", ExecutionKey = "execution", InstanceStatus = "Running", ProducedBy = "tests"
    };

    private sealed class FakeFanout : IRuntimeReactiveFanout
    {
        public event Action<RuntimeReactiveEvent> Received;
        public event Action ResyncRequired;
        public bool FailPublish { get; init; }
        public RuntimeReactiveEvent LastPublished { get; private set; }
        public Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        {
            LastPublished = eventData;
            return FailPublish ? Task.FromException(new InvalidOperationException("offline")) : Task.CompletedTask;
        }
        public void Deliver(RuntimeReactiveEvent eventData) => Received?.Invoke(eventData);
        public void Resync() => ResyncRequired?.Invoke();
    }

    private sealed class FakeHealthFanout : IRuntimeReactiveFanout, IRuntimeReactiveFanoutHealth
    {
        public event Action<RuntimeReactiveEvent> Received;
        public event Action ResyncRequired;
        public event Action<bool> SynchronizationChanged;
        public bool IsSynchronized { get; init; }
        public Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ChangeSynchronization(bool synchronized) => SynchronizationChanged?.Invoke(synchronized);
    }
}
