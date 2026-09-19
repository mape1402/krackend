namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;
using Mule;
using Mule.Dispatching;
using NSubstitute;

public sealed class MuleBufferingTests
{
    [Fact]
    public async Task IntakeBufferMule_WhenWorkItemIsTrigger_EnqueuesTriggerActionWithCorrelation()
    {
        var muleClient = Substitute.For<IMuleClient>();
        ActionKey? capturedKey = null;
        WorkItem? capturedPayload = null;
        Action<EnqueueOptions>? capturedOptions = null;
        muleClient
            .EnqueueAsync(
                Arg.Do<ActionKey>(key => capturedKey = key),
                Arg.Do<WorkItem>(payload => capturedPayload = payload),
                Arg.Do<Action<EnqueueOptions>>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Guid>(Guid.NewGuid()));
        var buffer = BuildServiceProvider(muleClient).GetRequiredService<IIntakeBuffer>();
        var workItem = new WorkItem
        {
            ArtifactId = "artifact-1",
            IngressKind = IngressKind.Trigger,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                CorrelationId = "correlation-1",
                SagaId = "saga-1"
            }
        };

        await buffer.EnqueueWorkAsync(workItem, CancellationToken.None);

        var options = new EnqueueOptions();
        capturedOptions!(options);
        Assert.Equal("TriggerSaga", capturedKey!.ToString());
        Assert.Same(workItem, capturedPayload);
        Assert.Equal(MuleSettings.DefaultLane, options.Lane);
        Assert.Equal("correlation-1", options.CorrelationId);
        Assert.Equal("trigger:artifact-1:correlation-1", options.DeduplicationKey);
    }

    [Theory]
    [InlineData("dispatch-1", "task-1", 2, "backchannel:dispatch-1:2")]
    [InlineData("", "task-1", 3, "backchannel:task-1:3")]
    public async Task IntakeBufferMule_WhenWorkItemIsBackchannel_EnqueuesBackchannelActionWithDeduplication(
        string dispatchId,
        string taskExecutionId,
        int attempt,
        string expectedDeduplicationKey)
    {
        var muleClient = Substitute.For<IMuleClient>();
        ActionKey? capturedKey = null;
        Action<EnqueueOptions>? capturedOptions = null;
        muleClient
            .EnqueueAsync(
                Arg.Do<ActionKey>(key => capturedKey = key),
                Arg.Any<WorkItem>(),
                Arg.Do<Action<EnqueueOptions>>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Guid>(Guid.NewGuid()));
        var buffer = BuildServiceProvider(muleClient).GetRequiredService<IIntakeBuffer>();
        var workItem = new WorkItem
        {
            ArtifactId = "artifact-1",
            IngressKind = IngressKind.Backchannel,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = "instance-1",
                CorrelationId = "correlation-1",
                DispatchId = dispatchId,
                TaskExecutionId = taskExecutionId,
                Attempt = attempt
            }
        };

        await buffer.EnqueueWorkAsync(workItem, CancellationToken.None);

        var options = new EnqueueOptions();
        capturedOptions!(options);
        Assert.Equal("BackchannelSaga", capturedKey!.ToString());
        Assert.Equal(MuleSettings.DefaultLane, options.Lane);
        Assert.Equal("instance-1", options.CorrelationId);
        Assert.Equal(expectedDeduplicationKey, options.DeduplicationKey);
    }

    [Fact]
    public async Task IntakeBufferMule_WhenWorkItemKindIsUnsupported_IgnoresIt()
    {
        var muleClient = Substitute.For<IMuleClient>();
        var buffer = BuildServiceProvider(muleClient).GetRequiredService<IIntakeBuffer>();
        var workItem = new WorkItem
        {
            ArtifactId = "artifact-1",
            IngressKind = (IngressKind)999
        };

        await buffer.EnqueueWorkAsync(workItem, CancellationToken.None);

        await muleClient.DidNotReceiveWithAnyArgs().EnqueueAsync<WorkItem>(
            default!,
            default!,
            default!,
            default);
    }

    [Fact]
    public async Task IntakeBufferMule_WhenBackchannelHasNoMetadata_EnqueuesWithoutDeduplication()
    {
        var muleClient = Substitute.For<IMuleClient>();
        Action<EnqueueOptions>? capturedOptions = null;
        muleClient
            .EnqueueAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<WorkItem>(),
                Arg.Do<Action<EnqueueOptions>>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Guid>(Guid.NewGuid()));
        var buffer = BuildServiceProvider(muleClient).GetRequiredService<IIntakeBuffer>();
        var workItem = new WorkItem
        {
            ArtifactId = "artifact-1",
            IngressKind = IngressKind.Backchannel,
            MessageMetadata = null
        };

        await buffer.EnqueueWorkAsync(workItem, CancellationToken.None);

        var options = new EnqueueOptions();
        capturedOptions!(options);
        Assert.Null(options.CorrelationId);
        Assert.Null(options.DeduplicationKey);
    }

    [Fact]
    public async Task MuleBufferingServicesRejectNullWorkAndCommands()
    {
        var provider = BuildServiceProvider(Substitute.For<IMuleClient>());
        var buffer = provider.GetRequiredService<IIntakeBuffer>();
        var dispatcher = provider.GetRequiredService<IRemoteCommandDispatcher>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => buffer.EnqueueWorkAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task IntakeBufferMule_WhenMuleClientFails_ThrowsForTransportRedelivery()
    {
        var muleClient = Substitute.For<IMuleClient>();
        muleClient
            .EnqueueAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<WorkItem>(),
                Arg.Any<Action<EnqueueOptions>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<Guid>(Task.FromException<Guid>(new InvalidOperationException("redis unavailable"))));
        var buffer = BuildServiceProvider(muleClient).GetRequiredService<IIntakeBuffer>();
        var workItem = new WorkItem
        {
            ArtifactId = "artifact-1",
            IngressKind = IngressKind.Trigger,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                CorrelationId = "correlation-1"
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            buffer.EnqueueWorkAsync(workItem, CancellationToken.None));

        await muleClient.Received(1).EnqueueAsync(
            Arg.Is<ActionKey>(key => key.ToString() == "TriggerSaga"),
            workItem,
            Arg.Any<Action<EnqueueOptions>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MuleRemoteCommandDispatcher_WhenCommandIsImmediate_EnqueuesRemoteDispatchAction()
    {
        var muleClient = Substitute.For<IMuleClient>();
        Action<EnqueueOptions>? capturedOptions = null;
        muleClient
            .EnqueueAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<RemoteCommand>(),
                Arg.Do<Action<EnqueueOptions>>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Guid>(Guid.NewGuid()));
        var dispatcher = BuildServiceProvider(
                muleClient,
                Substitute.For<IMuleStorage>(),
                Substitute.For<IMuleSerializer>(),
                Substitute.For<IMuleCommitNotifier>())
            .GetRequiredService<IRemoteCommandDispatcher>();
        var command = new RemoteCommand
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionAttemptId = "attempt-1",
            DispatchId = "dispatch-1"
        };

        await dispatcher.DispatchAsync(command, CancellationToken.None);

        await muleClient.Received(1).EnqueueAsync(
            Arg.Is<ActionKey>(key => key.ToString() == "RemoteCommandDispatch"),
            command,
            Arg.Any<Action<EnqueueOptions>>(),
            Arg.Any<CancellationToken>());
        var options = new EnqueueOptions();
        capturedOptions!(options);
        Assert.Equal("instance-1", options.CorrelationId);
        Assert.Equal("dispatch-1", options.DeduplicationKey);
    }

    [Fact]
    public async Task MuleRemoteCommandDispatcher_WhenCommandHasNoDispatchState_UsesTaskSpecificDeduplication()
    {
        var muleClient = Substitute.For<IMuleClient>();
        Action<EnqueueOptions>? capturedOptions = null;
        muleClient
            .EnqueueAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<RemoteCommand>(),
                Arg.Do<Action<EnqueueOptions>>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<Guid>(Guid.NewGuid()));
        var dispatcher = BuildServiceProvider(
                muleClient,
                Substitute.For<IMuleStorage>(),
                Substitute.For<IMuleSerializer>(),
                Substitute.For<IMuleCommitNotifier>())
            .GetRequiredService<IRemoteCommandDispatcher>();
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            TaskKey = "inventories.reserve",
            SettingsPayload = """{"topic":"inventories.release","version":"1.0.0"}"""
        };

        await dispatcher.DispatchAsync(command, CancellationToken.None);

        var options = new EnqueueOptions();
        capturedOptions!(options);
        Assert.Equal("instance-1", options.CorrelationId);
        Assert.Equal(
            """instance-1:task-1:inventories.reserve:Messaging:{"topic":"inventories.release","version":"1.0.0"}""",
            options.DeduplicationKey);
    }

    [Fact]
    public async Task MuleRemoteCommandDispatcher_WhenCommandIsScheduled_StoresDurableActionAndNotifiesCommit()
    {
        DurableAction? storedAction = null;
        var scheduledOnUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var muleStorage = Substitute.For<IMuleStorage>();
        muleStorage
            .AddAsync(Arg.Do<DurableAction>(action => storedAction = action), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        muleStorage.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        muleStorage
            .FindByDeduplicationKeyAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => storedAction!.Id);
        var serializer = Substitute.For<IMuleSerializer>();
        serializer.Serialize(Arg.Any<RemoteCommand>()).Returns("{\"serialized\":true}");
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        var dispatcher = BuildServiceProvider(
                Substitute.For<IMuleClient>(),
                muleStorage,
                serializer,
                commitNotifier)
            .GetRequiredService<IRemoteCommandDispatcher>();
        var command = new RemoteCommand
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionAttemptId = "attempt-1",
            DispatchId = "dispatch-1",
            ScheduledOnUtc = scheduledOnUtc
        };

        await dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.NotNull(storedAction);
        Assert.Equal("RemoteCommandDispatch", storedAction!.Key.ToString());
        Assert.Equal(MuleSettings.DefaultLane, storedAction.Lane);
        Assert.Equal("{\"serialized\":true}", storedAction.Payload);
        Assert.Equal(typeof(RemoteCommand).AssemblyQualifiedName, storedAction.PayloadType);
        Assert.Equal("instance-1", storedAction.CorrelationId);
        Assert.Equal("dispatch-1", storedAction.DeduplicationKey);
        Assert.Equal(DurableActionStatus.Pending, storedAction.Status);
        Assert.Equal(scheduledOnUtc, storedAction.NextAttemptOnUtc);
        await muleStorage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await commitNotifier.Received(1).NotifySavedAsync(
            storedAction.Id,
            MuleSettings.DefaultLane,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MuleRemoteCommandDispatcher_WhenScheduledCommandBecomesDue_RenotifiesCommit()
    {
        DurableAction? storedAction = null;
        var scheduledOnUtc = DateTimeOffset.UtcNow.AddSeconds(1);
        var muleStorage = Substitute.For<IMuleStorage>();
        muleStorage
            .AddAsync(Arg.Do<DurableAction>(action => storedAction = action), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        muleStorage.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        muleStorage
            .FindByDeduplicationKeyAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => storedAction!.Id);
        var serializer = Substitute.For<IMuleSerializer>();
        serializer.Serialize(Arg.Any<RemoteCommand>()).Returns("{\"serialized\":true}");
        var commitNotifier = Substitute.For<IMuleCommitNotifier>();
        var dispatcher = BuildServiceProvider(
                Substitute.For<IMuleClient>(),
                muleStorage,
                serializer,
                commitNotifier)
            .GetRequiredService<IRemoteCommandDispatcher>();
        var command = new RemoteCommand
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionAttemptId = "attempt-1",
            DispatchId = "dispatch-1",
            ScheduledOnUtc = scheduledOnUtc
        };

        await dispatcher.DispatchAsync(command, CancellationToken.None);
        await WaitUntilAsync(async () =>
            await commitNotifier.Received(2).NotifySavedAsync(
                storedAction!.Id,
                MuleSettings.DefaultLane,
                Arg.Any<CancellationToken>()));
    }

    private static ServiceProvider BuildServiceProvider(
        IMuleClient muleClient,
        IMuleStorage? muleStorage = null,
        IMuleSerializer? muleSerializer = null,
        IMuleCommitNotifier? commitNotifier = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddKrackendOrchestrationsRuntime()
            .AddMule(_ => { });
        services.AddSingleton(muleClient);
        services.AddSingleton(muleStorage ?? Substitute.For<IMuleStorage>());
        services.AddSingleton(muleSerializer ?? Substitute.For<IMuleSerializer>());
        services.AddSingleton(commitNotifier ?? Substitute.For<IMuleCommitNotifier>());
        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<Task> assertion)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await assertion();
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(25);
            }
        }

        throw new TimeoutException("The expected condition was not reached.", lastException);
    }
}
