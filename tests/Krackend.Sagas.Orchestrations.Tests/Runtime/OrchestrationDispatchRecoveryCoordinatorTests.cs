namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using NSubstitute;

public sealed class OrchestrationDispatchRecoveryCoordinatorTests
{
    [Fact]
    public async Task RecoverPendingDispatchesRequeuesPersistedCommandWithoutCreatingAnotherAttempt()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        state.Dispatch.Metadata["Runtime.Delivery.ProviderKey"] = JsonValue.Create("builtin");
        state.Dispatch.Metadata["Runtime.Delivery.Command"] = Serialize(CreateCommand(state));
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Enqueued", state.Dispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.Running, state.Task.Status);
        Assert.Equal(TaskExecutionStatus.Running, state.Attempt.Status);
        Assert.Single(fixture.Provider.RecoveryRequests);
        Assert.Equal(state.Dispatch.Id.ToString(), fixture.Provider.RecoveryRequests[0].Command.DispatchId);
        Assert.Equal("TaskDispatchRecovered", Assert.Single(fixture.Transitions).TransitionType);
        await fixture.SagaEngine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
        await fixture.LeaseCoordinator.Received(1).TryExecuteAsync(
            state.Instance.Id,
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>(),
            waitForLease: false);
    }

    [Fact]
    public async Task RecoverPendingDispatchesMarksFailureAndAdvancesTheSagaWhenProviderRejectsDelivery()
    {
        var fixture = new RecoveryFixture();
        fixture.Provider.Result = ExecutionDispatchRecoveryResult.Rejected("Unavailable", "Mule storage is unavailable.");
        var state = CreateState();
        state.Dispatch.Metadata["Runtime.Delivery.ProviderKey"] = JsonValue.Create("builtin");
        state.Dispatch.Metadata["Runtime.Delivery.Command"] = Serialize(CreateCommand(state));
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Failed", state.Dispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.Failed, state.Task.Status);
        Assert.Equal(TaskExecutionStatus.Failed, state.Attempt.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, state.Instance.Status);
        Assert.Equal("Unavailable", state.Attempt.ErrorCode);
        Assert.Equal("TaskDispatchRecoveryFailed", Assert.Single(fixture.Transitions).TransitionType);
        await fixture.SagaEngine.Received(1).OrchestrateAsync(
            Arg.Is<ForwardIntent>(intent => intent.MessageMetadata.OrchestrationInstanceId == state.Instance.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverPendingDispatchesRetainsIntentWhenProviderCannotConfirmDelivery()
    {
        var fixture = new RecoveryFixture();
        fixture.Provider.Exception = new RemoteCommandDeliveryUncertainException(
            "The durable queue did not confirm the command.",
            new TimeoutException());
        var state = CreateState();
        state.Dispatch.Metadata["Runtime.Delivery.ProviderKey"] = JsonValue.Create("builtin");
        state.Dispatch.Metadata["Runtime.Delivery.Command"] = Serialize(CreateCommand(state));
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("QueuePending", state.Dispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.Running, state.Task.Status);
        Assert.Equal(TaskExecutionStatus.Running, state.Attempt.Status);
        Assert.Equal("The durable queue did not confirm the command.", state.Dispatch.Metadata["Runtime.Delivery.LastRecoveryFailure"]!.GetValue<string>());
        Assert.Equal("TaskDispatchRecoveryDeferred", Assert.Single(fixture.Transitions).TransitionType);
        await fixture.SagaEngine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task RecoverPendingDispatchesFailsMissingSnapshotThroughTheNormalSagaPolicyPath()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Failed", state.Dispatch.DispatchStatus);
        Assert.Equal("DispatchRecoverySnapshotMissing", state.Attempt.ErrorCode);
        Assert.Empty(fixture.Provider.RecoveryRequests);
        await fixture.SagaEngine.Received(1).OrchestrateAsync(Arg.Any<ForwardIntent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverPendingDispatchesFencesDeliveryThatNoLongerBelongsToTheCurrentAttempt()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        state.Task.Status = TaskExecutionStatus.Completed;
        state.Dispatch.Metadata["Runtime.Delivery.ProviderKey"] = JsonValue.Create("builtin");
        state.Dispatch.Metadata["Runtime.Delivery.Command"] = Serialize(CreateCommand(state));
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Cancelled", state.Dispatch.DispatchStatus);
        Assert.Empty(fixture.Provider.RecoveryRequests);
        Assert.Equal("TaskDispatchRecoverySuppressed", Assert.Single(fixture.Transitions).TransitionType);
        await fixture.SagaEngine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task RecoverPendingDispatchesSkipsBusyInstancesWithoutWaitingForTheirLease()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        state.Dispatch.Metadata["Runtime.Delivery.ProviderKey"] = JsonValue.Create("builtin");
        state.Dispatch.Metadata["Runtime.Delivery.Command"] = Serialize(CreateCommand(state));
        fixture.Arrange(state);
        fixture.LeaseCoordinator.TryExecuteAsync(
                Arg.Any<Id>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>(),
                waitForLease: false)
            .Returns(Task.FromResult(false));

        var handled = await fixture.Coordinator.RecoverPendingDispatchesAsync(10);

        Assert.Equal(0, handled);
        Assert.Equal("QueuePending", state.Dispatch.DispatchStatus);
        Assert.Empty(fixture.Provider.RecoveryRequests);
    }

    private static JsonNode Serialize(RemoteCommand command)
        => JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static RecoveryState CreateState()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders.created",
            RuntimeOrchestrationArtifactId = Id.New(),
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "orders.created:corr-1",
            Status = OrchestrationInstanceStatus.Running,
            CurrentStageKey = "fulfillment",
            CurrentTaskKey = "reserve"
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "reserve",
            Status = TaskExecutionStatus.Running,
            LastAttemptNumber = 1,
            OnErrorPolicy = OnErrorPolicy.Stop
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "QueuePending",
            ScheduledOnUtc = DateTime.UtcNow,
            CorrelationId = instance.CorrelationId
        };
        attempt.DispatchId = dispatch.Id;
        return new RecoveryState(instance, task, attempt, dispatch);
    }

    private static RemoteCommand CreateCommand(RecoveryState state)
        => new()
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{\"orderId\":\"order-1\"}",
            SettingsPayload = "{\"topic\":\"commands.inventory.reserve\"}",
            OrchestrationInstanceId = state.Instance.Id.ToString(),
            StageExecutionId = state.Task.StageExecutionId.ToString(),
            TaskExecutionId = state.Task.Id.ToString(),
            TaskExecutionAttemptId = state.Attempt.Id.ToString(),
            DispatchId = state.Dispatch.Id.ToString(),
            StageKey = "fulfillment",
            TaskKey = state.Task.TaskKey,
            AwaitResponse = true
        };

    private sealed record RecoveryState(
        OrchestrationInstance Instance,
        TaskExecution Task,
        TaskExecutionAttempt Attempt,
        TaskDispatch Dispatch);

    private sealed class RecoveryFixture
    {
        public RecoveryFixture()
        {
            LeaseCoordinator.TryExecuteAsync(
                    Arg.Any<Id>(),
                    Arg.Any<Func<CancellationToken, Task>>(),
                    Arg.Any<CancellationToken>(),
                    waitForLease: false)
                .Returns(async call =>
                {
                    await call.ArgAt<Func<CancellationToken, Task>>(1)(call.ArgAt<CancellationToken>(2));
                    return true;
                });
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            SagaEngine.OrchestrateAsync(Arg.Any<ForwardIntent>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Coordinator = new DefaultOrchestrationDispatchRecoveryCoordinator(
                DispatchRepository,
                AttemptRepository,
                TaskRepository,
                InstanceRepository,
                TransitionRepository,
                new NoopRuntimeStorageUnitOfWork(),
                new ExecutionSandboxProviderRegistry([Provider]),
                LeaseCoordinator,
                SagaEngine);
        }

        public ITaskDispatchRepository DispatchRepository { get; } = Substitute.For<ITaskDispatchRepository>();

        public ITaskExecutionAttemptRepository AttemptRepository { get; } = Substitute.For<ITaskExecutionAttemptRepository>();

        public ITaskExecutionRepository TaskRepository { get; } = Substitute.For<ITaskExecutionRepository>();

        public IOrchestrationInstanceRepository InstanceRepository { get; } = Substitute.For<IOrchestrationInstanceRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } = Substitute.For<IExecutionTransitionRepository>();

        public IOrchestrationInstanceCoordinator LeaseCoordinator { get; } = Substitute.For<IOrchestrationInstanceCoordinator>();

        public ISagaEngine SagaEngine { get; } = Substitute.For<ISagaEngine>();

        public RecoverableProvider Provider { get; } = new();

        public List<ExecutionTransition> Transitions { get; } = [];

        public DefaultOrchestrationDispatchRecoveryCoordinator Coordinator { get; }

        public void Arrange(RecoveryState state)
        {
            DispatchRepository.GetQueuePending(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([state.Dispatch]));
            DispatchRepository.TryGetById(state.Dispatch.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Dispatch));
            AttemptRepository.GetById(state.Attempt.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Attempt));
            TaskRepository.GetById(state.Task.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Task));
            InstanceRepository.GetById(state.Instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Instance));
            DispatchRepository.Update(Arg.Any<TaskDispatch>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            AttemptRepository.Update(Arg.Any<TaskExecutionAttempt>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            TaskRepository.Update(Arg.Any<TaskExecution>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            InstanceRepository.Update(Arg.Any<OrchestrationInstance>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }
    }

    private sealed class RecoverableProvider : IExecutionSandboxProvider, IRecoverableExecutionSandboxProvider
    {
        public string ProviderKey => "builtin";

        public string ExecutionMode => "local";

        public bool IsSandbox => false;

        public ExecutionDispatchRecoveryResult Result { get; set; } =
            ExecutionDispatchRecoveryResult.Accepted("Accepted", "Durable delivery accepted.");

        public List<ExecutionDispatchRecoveryRequest> RecoveryRequests { get; } = [];

        public Exception? Exception { get; set; }

        public Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ExecutionDispatchRecoveryResult> RecoverDispatchAsync(
            ExecutionDispatchRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            RecoveryRequests.Add(request);
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }
}
