namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using NSubstitute;

public sealed class OrchestrationCompensationRecoveryCoordinatorTests
{
    [Fact]
    public async Task RecoverPendingCompensationsDispatchesThePersistedCommandAndCompletesTheIntent()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Completed", state.Compensation.Status);
        Assert.NotNull(state.Compensation.CompletedOnUtc);
        await fixture.Dispatcher.Received(1).DispatchAsync(
            Arg.Is<RemoteCommand>(command =>
                command.Payload == "{\"orderId\":\"order-1\"}" &&
                command.TaskKey == "inventory_undo"),
            Arg.Any<CancellationToken>());
        Assert.Equal("CompensationDispatchRecovered", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task RecoverPendingCompensationsRetainsTheIntentWhenDeliveryCannotBeConfirmed()
    {
        var fixture = new RecoveryFixture();
        fixture.Dispatcher.DispatchAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new RemoteCommandDeliveryUncertainException(
                "Mule did not confirm the compensation delivery.",
                new TimeoutException())));
        var state = CreateState();
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Pending", state.Compensation.Status);
        Assert.Equal(
            "Mule did not confirm the compensation delivery.",
            state.Compensation.Metadata["Runtime.Compensation.Delivery.LastFailure"]!.GetValue<string>());
        Assert.Equal("CompensationDispatchRecoveryDeferred", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task RecoverPendingCompensationsMarksATerminalCompensationFailureOnTheInstance()
    {
        var fixture = new RecoveryFixture();
        fixture.Dispatcher.DispatchAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Compensation endpoint rejected the request.")));
        var state = CreateState(terminalFailure: true);
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Failed", state.Compensation.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, state.Instance.Status);
        Assert.True(state.Instance.Metadata["Compensation.TerminalFailure"]!.GetValue<bool>());
        Assert.Equal("CompensationRecoveryDispatchFailed", state.Compensation.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        Assert.Equal("CompensationDispatchRecoveryFailed", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task RecoverPendingCompensationsSuppressesAnIntentWhoseInstanceIsNoLongerCompensating()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        state.Instance.Status = OrchestrationInstanceStatus.Compensated;
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Cancelled", state.Compensation.Status);
        await fixture.Dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
        Assert.Equal("CompensationDispatchRecoverySuppressed", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task RecoverPendingCompensationsFailsAnIntentWithoutADurableCommandSnapshot()
    {
        var fixture = new RecoveryFixture();
        var state = CreateState();
        state.Compensation.Metadata.Remove("Runtime.Compensation.Delivery.Command");
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(1, handled);
        Assert.Equal("Failed", state.Compensation.Status);
        Assert.Equal("CompensationRecoverySnapshotMissing", state.Compensation.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        await fixture.Dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
        Assert.Equal("CompensationDispatchRecoveryFailed", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task RecoverPendingCompensationsSkipsAnInstanceWhoseLeaseIsBusy()
    {
        var fixture = new RecoveryFixture();
        fixture.LeaseCoordinator.TryExecuteAsync(
                Arg.Any<Id>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>(),
                waitForLease: false)
            .Returns(Task.FromResult(false));
        var state = CreateState();
        fixture.Arrange(state);

        var handled = await fixture.Coordinator.RecoverPendingCompensationsAsync(10);

        Assert.Equal(0, handled);
        Assert.Equal("Pending", state.Compensation.Status);
        await fixture.Dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
    }

    private static RecoveryState CreateState(bool terminalFailure = false)
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders_created",
            RuntimeOrchestrationArtifactId = Id.New(),
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "orders_created:corr-1",
            Status = OrchestrationInstanceStatus.Compensating
        };
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{\"orderId\":\"order-1\"}",
            SettingsPayload = "{\"topic\":\"commands.inventory.undo\"}",
            OrchestrationInstanceId = instance.Id.ToString(),
            StageExecutionId = Id.New().ToString(),
            TaskExecutionId = Id.New().ToString(),
            TaskKey = "inventory_undo",
            AwaitResponse = false
        };
        var compensation = new CompensationExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            SourceTaskExecutionId = Id.New(),
            CompensationTaskKey = "commands.inventory.undo",
            Status = "Pending",
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            RequestPayload = JsonNode.Parse(command.Payload)
        };
        compensation.Metadata["Runtime.Compensation.Delivery.Command"] = JsonSerializer.SerializeToNode(
            command,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        compensation.Metadata["Runtime.Compensation.Delivery.TerminalFailure"] = JsonValue.Create(terminalFailure);
        return new RecoveryState(instance, compensation);
    }

    private sealed record RecoveryState(OrchestrationInstance Instance, CompensationExecution Compensation);

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
            Dispatcher.DispatchAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            CompensationRepository.Update(Arg.Any<CompensationExecution>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            InstanceRepository.Update(Arg.Any<OrchestrationInstance>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Coordinator = new DefaultOrchestrationCompensationRecoveryCoordinator(
                CompensationRepository,
                InstanceRepository,
                TransitionRepository,
                Dispatcher,
                new RuntimeStateTransaction(new NoopRuntimeStorageUnitOfWork()),
                LeaseCoordinator);
        }

        public ICompensationExecutionRepository CompensationRepository { get; } = Substitute.For<ICompensationExecutionRepository>();

        public IOrchestrationInstanceRepository InstanceRepository { get; } = Substitute.For<IOrchestrationInstanceRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } = Substitute.For<IExecutionTransitionRepository>();

        public IRemoteCommandDispatcher Dispatcher { get; } = Substitute.For<IRemoteCommandDispatcher>();

        public IOrchestrationInstanceCoordinator LeaseCoordinator { get; } = Substitute.For<IOrchestrationInstanceCoordinator>();

        public List<ExecutionTransition> Transitions { get; } = [];

        public DefaultOrchestrationCompensationRecoveryCoordinator Coordinator { get; }

        public void Arrange(RecoveryState state)
        {
            CompensationRepository.GetPending(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<CompensationExecution>>([state.Compensation]));
            CompensationRepository.TryGetById(state.Compensation.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Compensation));
            InstanceRepository.GetById(state.Instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(state.Instance));
        }
    }
}
