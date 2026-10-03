namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using NSubstitute;

public sealed class MessagingTaskRuntimeAdapterTests
{
    private static readonly SemanticVersion Version = new(1, 0, 0);

    [Fact]
    public void ValidateTaskAndCompensationCoverSupportedAndRejectedShapes()
    {
        var adapter = new MessagingTaskRuntimeAdapter();
        var task = MessagingTask();
        var compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget);

        Assert.True(adapter.ValidateTask(task, "stage-one").Succeeded);
        Assert.Equal("commands.sales.reserve:1.0.0", adapter.GetDestination(task));
        Assert.True(adapter.ValidateCompensation(task with { Compensation = null! }, "stage-one").Succeeded);
        Assert.True(adapter.ValidateCompensation(task with { Compensation = compensation with { Configuration = null! } }, "stage-one").Succeeded);
        Assert.True(adapter.ValidateCompensation(task with { Compensation = compensation }, "stage-one").Succeeded);
        Assert.Equal("commands.sales.undo:1.0.0", adapter.GetCompensationDestination(compensation));

        AssertFailure(
            adapter.ValidateTask(task with { Configuration = new HumanApprovalTaskConfigurationArtifact() }, "stage-one"),
            "TaskConfigurationNotSupported");
        AssertFailure(
            adapter.ValidateTask(task with { Configuration = MessagingConfiguration(" ") }, "stage-one"),
            "MessagingTopicMissing");
        AssertFailure(
            adapter.ValidateTask(task with { DispatchType = TaskDispatchType.FireAndWait }, "stage-one"),
            "MessagingDispatchTypeNotSupported");
        AssertFailure(
            adapter.ValidateCompensation(task with { Compensation = compensation with { CompensationTaskKind = TaskKind.Http } }, "stage-one"),
            "CompensationTaskKindNotSupported");
        AssertFailure(
            adapter.ValidateCompensation(task with { Compensation = compensation with { Configuration = new HumanApprovalTaskConfigurationArtifact() } }, "stage-one"),
            "CompensationConfigurationNotSupported");
        AssertFailure(
            adapter.ValidateCompensation(task with { Compensation = compensation with { Configuration = MessagingConfiguration(" ") } }, "stage-one"),
            "CompensationMessagingTopicMissing");
        AssertFailure(
            adapter.ValidateCompensation(task with { Compensation = compensation with { DispatchType = TaskDispatchType.FireAndWaitCallback } }, "stage-one"),
            "CompensationDispatchTypeNotSupported");
        Assert.Throws<InvalidOperationException>(() => adapter.GetDestination(task with { Configuration = new HumanApprovalTaskConfigurationArtifact() }));
        Assert.Throws<InvalidOperationException>(() => adapter.GetCompensationDestination(compensation with { Configuration = new HumanApprovalTaskConfigurationArtifact() }));
    }

    [Fact]
    public async Task BuildCommandsSerializeMessagingPayloadsAndResolveBackchannelReplyAddress()
    {
        MessagingCommand? captured = null;
        var serializer = Substitute.For<IMessagingCommandSerializer>();
        serializer.Serialize(Arg.Do<MessagingCommand>(command => captured = command))
            .Returns("serialized-command");
        var accessor = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        accessor.GetConfigurationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(
            [
                new()
                {
                    Id = "trigger",
                    ArtifactId = "artifact",
                    IngressKind = IngressKind.Trigger,
                    IngressTransport = IngressTransport.Messaging,
                    SettingsPayload = "trigger-settings"
                },
                new()
                {
                    Id = "backchannel",
                    ArtifactId = "artifact",
                    IngressKind = IngressKind.Backchannel,
                    IngressTransport = IngressTransport.Messaging,
                    SettingsPayload = "backchannel-settings"
                }
            ]));
        var adapter = new MessagingTaskRuntimeAdapter(serializer, accessor);

        var command = await adapter.BuildCommandAsync(new TaskRuntimeCommandRequest
        {
            Instance = Instance(),
            StageExecutionId = Id.New(),
            StageKey = "stage-one",
            Task = MessagingTask(TaskDispatchType.FireAndWaitCallback),
            TaskExecution = TaskExecution(),
            Attempt = new TaskExecutionAttempt { Id = Id.New() },
            Dispatch = Dispatch(),
            Payload = """{"saleId":"sale-1"}"""
        });
        var compensation = await adapter.BuildCompensationCommandAsync(new TaskRuntimeCompensationCommandRequest
        {
            Instance = Instance(),
            SourceTaskExecution = TaskExecution(),
            CompensationExecution = CompensationExecution(),
            Compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget),
            Payload = """{"undo":true}"""
        });

        Assert.Equal(RemoteCommandTransport.Messaging, command.Transport);
        Assert.Equal("serialized-command", command.SettingsPayload);
        Assert.Equal("messaging", command.ReplyAddress!.Transport);
        Assert.Equal("backchannel-settings", command.ReplyAddress.SettingsPayload);
        Assert.Equal("commands.sales.undo", captured!.Topic);
        Assert.Equal("1.0.0", captured.Version);
        Assert.Equal("""{"undo":true}""", captured.Payload);
        Assert.Equal(RemoteCommandTransport.Messaging, compensation.Transport);
        Assert.Equal("backchannel-settings", compensation.ReplyAddress!.SettingsPayload);
    }

    [Fact]
    public async Task BuildCommandHandlesFireAndForgetWithoutBackchannelAndRejectsMissingRuntimeDependencies()
    {
        var serializer = Substitute.For<IMessagingCommandSerializer>();
        serializer.Serialize(Arg.Any<MessagingCommand>()).Returns("serialized-command");
        var accessor = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        accessor.GetConfigurationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([]));
        var adapter = new MessagingTaskRuntimeAdapter(serializer, accessor);
        var request = new TaskRuntimeCommandRequest
        {
            Instance = Instance(),
            StageExecutionId = Id.New(),
            StageKey = "stage-one",
            Task = MessagingTask(TaskDispatchType.FireAndForget),
            TaskExecution = TaskExecution(),
            Attempt = new TaskExecutionAttempt { Id = Id.New() },
            Dispatch = Dispatch(),
            Payload = "{}"
        };

        var descriptor = await adapter.BuildCommandAsync(request);

        Assert.Null(descriptor.ReplyAddress);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.BuildCommandAsync(new TaskRuntimeCommandRequest
        {
            Instance = request.Instance,
            StageExecutionId = request.StageExecutionId,
            StageKey = request.StageKey,
            Task = MessagingTask(TaskDispatchType.FireAndWaitCallback),
            TaskExecution = request.TaskExecution,
            Attempt = request.Attempt,
            Dispatch = request.Dispatch,
            Payload = request.Payload
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MessagingTaskRuntimeAdapter().BuildCommandAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MessagingTaskRuntimeAdapter().BuildCompensationCommandAsync(new TaskRuntimeCompensationCommandRequest
        {
            Instance = Instance(),
            SourceTaskExecution = TaskExecution(),
            CompensationExecution = CompensationExecution(),
            Compensation = Compensation("commands.sales.undo", TaskKind.Messaging, TaskDispatchType.FireAndForget),
            Payload = "{}"
        }));
        Assert.Throws<ArgumentNullException>(() => new MessagingTaskRuntimeAdapter(null!, accessor));
        Assert.Throws<ArgumentNullException>(() => new MessagingTaskRuntimeAdapter(serializer, null!));
    }

    private static void AssertFailure(RuntimeArtifactCompatibilityValidationResult result, string expectedCode)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.ErrorCode);
    }

    private static OrchestrationInstance Instance()
        => new()
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created:corr-1"
        };

    private static TaskExecution TaskExecution()
        => new()
        {
            Id = Id.New(),
            TaskKey = "sales.reserve"
        };

    private static TaskDispatch Dispatch()
        => new()
        {
            Id = Id.New(),
            DispatchType = "Messaging",
            DispatchStatus = "Pending"
        };

    private static CompensationExecution CompensationExecution()
        => new()
        {
            Id = Id.New(),
            CompensationTaskKey = "commands.sales.undo",
            Status = "Pending"
        };

    private static TaskArtifact MessagingTask(TaskDispatchType dispatchType = TaskDispatchType.FireAndWaitCallback)
        => new(
            Id.New(),
            "sales.reserve",
            "Reserve sale",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null!,
            null!,
            MessagingConfiguration("commands.sales.reserve"),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            dispatchType,
            true);

    private static CompensationArtifact Compensation(
        string topic,
        TaskKind taskKind,
        TaskDispatchType dispatchType)
        => new(
            taskKind,
            null!,
            null!,
            MessagingConfiguration(topic),
            null!,
            null!,
            dispatchType);

    private static MessagingTaskConfigurationArtifact MessagingConfiguration(string topic)
        => new(topic, Version, null);
}
