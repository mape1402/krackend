namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using NSubstitute;

public sealed class OrchestrationDispatchCancellationCoordinatorTests
{
    [Fact]
    public void CancellationResultPreservesItsOriginalConstructorAndDeconstruction()
    {
        var result = new TaskDispatchCancellationResult(Requested: true, Status: "sent", Message: "ok");
        var (requested, status, message) = result;

        Assert.Equal((true, "sent", "ok"), (requested, status, message));
        Assert.True(result.IsTerminal);
    }

    [Fact]
    public async Task CancelPendingCancelsQueuePreparedDispatchesBeforeTheyReachTheProvider()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var pendingTask = CreateTask(instance.Id, TaskExecutionStatus.Running);
        var waitingTask = CreateTask(instance.Id, TaskExecutionStatus.WaitingResponse);
        var pendingAttempt = CreateAttempt(pendingTask.Id, TaskExecutionStatus.Running);
        var waitingAttempt = CreateAttempt(waitingTask.Id, TaskExecutionStatus.WaitingResponse);
        var pendingDispatch = CreateDispatch(pendingAttempt.Id, "QueuePending");
        var waitingDispatch = CreateDispatch(waitingAttempt.Id, "WaitingResponse");
        fixture.Arrange(
            instance,
            [pendingTask, waitingTask],
            [pendingAttempt, waitingAttempt],
            [pendingDispatch, waitingDispatch]);
        fixture.CancellationService
            .CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchCancellationResult.RequestedResult("Requested", "Provider cancellation requested.")));

        var cancelled = await fixture.Coordinator.CancelPendingAsync(instance, "operator aborted the orchestration");

        Assert.Equal(2, cancelled);
        Assert.Equal("Cancelled", pendingDispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.Cancelled, pendingAttempt.Status);
        Assert.Equal(TaskExecutionStatus.Cancelled, pendingTask.Status);
        Assert.Equal("operator aborted the orchestration", pendingDispatch.FailureReason);
        Assert.Equal("Requested", pendingDispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Equal("WaitingResponse", waitingDispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, waitingAttempt.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, waitingTask.Status);
        var transition = Assert.Single(fixture.Transitions.Where(candidate => candidate.TransitionType == "TaskDispatchCancelled"));
        Assert.Equal("TaskDispatchCancelled", transition.TransitionType);
        Assert.Equal("QueuePending", transition.FromStatus);
        Assert.False(pendingDispatch.PhysicalCancellationPending);
        Assert.Equal(1, pendingDispatch.PhysicalCancellationAttemptCount);
        Assert.Contains(fixture.Transitions, candidate => candidate.TransitionType == "TaskDispatchPhysicalCancellationFinalized");
        await fixture.CancellationService.Received(1).CancelAsync(
            Arg.Is<TaskDispatchCancellationRequest>(request => request.Dispatch == pendingDispatch),
            Arg.Any<CancellationToken>());
        await fixture.CancellationService.Received(1).CancelAsync(
            Arg.Is<TaskDispatchCancellationRequest>(request => request.Dispatch == waitingDispatch),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelPendingKeepsTheLogicalFenceWhenTheProviderThrows()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Aborted);
        var task = CreateTask(instance.Id, TaskExecutionStatus.Running);
        var attempt = CreateAttempt(task.Id, TaskExecutionStatus.Running);
        var dispatch = CreateDispatch(attempt.Id, "Enqueued");
        fixture.Arrange(instance, [task], [attempt], [dispatch]);
        fixture.CancellationService
            .CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TaskDispatchCancellationResult>(new InvalidOperationException("provider unavailable")));

        var cancelled = await fixture.Coordinator.CancelPendingAsync(instance, "instance is terminal");

        Assert.Equal(1, cancelled);
        Assert.Equal("Cancelled", dispatch.DispatchStatus);
        Assert.True(dispatch.PhysicalCancellationPending);
        Assert.Equal(1, dispatch.PhysicalCancellationAttemptCount);
        Assert.Equal("ProviderError", dispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Equal("provider unavailable", dispatch.Metadata["Recovery.DispatchCancellationProviderMessage"]!.GetValue<string>());
        Assert.Contains(fixture.Transitions, candidate => candidate.TransitionType == "TaskDispatchPhysicalCancellationDeferred");
    }

    [Fact]
    public async Task CancelPendingPropagatesRuntimeCancellationAndLeavesTheFenceRecoverable()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Aborted);
        var task = CreateTask(instance.Id, TaskExecutionStatus.Running);
        var attempt = CreateAttempt(task.Id, TaskExecutionStatus.Running);
        var dispatch = CreateDispatch(attempt.Id, "Enqueued");
        fixture.Arrange(instance, [task], [attempt], [dispatch]);
        using var source = new CancellationTokenSource();
        fixture.CancellationService.CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => { source.Cancel(); return Task.FromCanceled<TaskDispatchCancellationResult>(source.Token); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Coordinator.CancelPendingAsync(instance, "instance aborted", source.Token));

        Assert.Equal("Cancelled", dispatch.DispatchStatus);
        Assert.True(dispatch.PhysicalCancellationPending);
        Assert.Equal(0, dispatch.PhysicalCancellationAttemptCount);
    }

    [Fact]
    public async Task CancelPendingPhysicallyCancelsEveryReferencedDeliveryWithoutRewritingCompletedOrTimedOutLogicalState()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Completed);
        var completedTask = CreateTask(instance.Id, TaskExecutionStatus.Completed);
        var timedOutTask = CreateTask(instance.Id, TaskExecutionStatus.TimedOut);
        var completedAttempt = CreateAttempt(completedTask.Id, TaskExecutionStatus.Completed);
        var timedOutAttempt = CreateAttempt(timedOutTask.Id, TaskExecutionStatus.TimedOut);
        var completedDispatch = CreateDispatch(completedAttempt.Id, "Completed");
        var timedOutDispatch = CreateDispatch(timedOutAttempt.Id, "TimedOut");
        fixture.Arrange(
            instance,
            [completedTask, timedOutTask],
            [completedAttempt, timedOutAttempt],
            [completedDispatch, timedOutDispatch]);
        fixture.CancellationService
            .CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchCancellationResult.FinalResult("Cancelled", "Mule action cancelled.")));

        var cancelled = await fixture.Coordinator.CancelPendingAsync(instance, "instance completed");

        Assert.Equal(2, cancelled);
        Assert.Equal("Completed", completedDispatch.DispatchStatus);
        Assert.Equal("TimedOut", timedOutDispatch.DispatchStatus);
        Assert.DoesNotContain(fixture.Transitions, candidate => candidate.TransitionType == "TaskDispatchCancelled");
        Assert.Equal(2, fixture.Transitions.Count(candidate => candidate.TransitionType == "TaskDispatchPhysicalCancellationFinalized"));
        await fixture.CancellationService.Received(2).CancelAsync(
            Arg.Any<TaskDispatchCancellationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilePendingRetriesOnlyThePhysicalCancellationAfterAProviderOutage()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var task = CreateTask(instance.Id, TaskExecutionStatus.Cancelled);
        var attempt = CreateAttempt(task.Id, TaskExecutionStatus.Cancelled);
        var dispatch = CreateDispatch(attempt.Id, "Cancelled");
        dispatch.PhysicalCancellationPending = true;
        dispatch.Metadata["Recovery.DispatchCancellationReason"] = System.Text.Json.Nodes.JsonValue.Create("terminal instance");
        fixture.Arrange(instance, [task], [attempt], [dispatch]);
        fixture.CancellationRepository
            .GetPhysicalCancellationPending(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([dispatch]));
        fixture.CancellationService
            .CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchCancellationResult.FinalResult("Cancelled", "Mule action cancelled.")));

        var reconciled = await fixture.Coordinator.ReconcilePendingAsync(20);

        Assert.Equal(1, reconciled);
        Assert.False(dispatch.PhysicalCancellationPending);
        Assert.Equal(1, dispatch.PhysicalCancellationAttemptCount);
        Assert.Equal("Cancelled", dispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Contains(fixture.Transitions, candidate => candidate.TransitionType == "TaskDispatchPhysicalCancellationFinalized");
        await fixture.CancellationService.Received(1).CancelAsync(
            Arg.Is<TaskDispatchCancellationRequest>(request => request.Dispatch == dispatch && request.Reason == "terminal instance"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcilePendingLeavesADeferredCancellationForTheNextRuntimeCycle()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Aborted);
        var task = CreateTask(instance.Id, TaskExecutionStatus.Cancelled);
        var attempt = CreateAttempt(task.Id, TaskExecutionStatus.Cancelled);
        var dispatch = CreateDispatch(attempt.Id, "Cancelled");
        dispatch.PhysicalCancellationPending = true;
        fixture.Arrange(instance, [task], [attempt], [dispatch]);
        fixture.CancellationRepository
            .GetPhysicalCancellationPending(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([dispatch]));
        fixture.CancellationService
            .CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchCancellationResult.Deferred("ActionReferencePending", "Awaiting durable action reference.")));

        var reconciled = await fixture.Coordinator.ReconcilePendingAsync(20);

        Assert.Equal(1, reconciled);
        Assert.True(dispatch.PhysicalCancellationPending);
        Assert.Equal(1, dispatch.PhysicalCancellationAttemptCount);
        Assert.Equal("ActionReferencePending", dispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Contains(fixture.Transitions, candidate => candidate.TransitionType == "TaskDispatchPhysicalCancellationDeferred");
    }

    [Theory]
    [InlineData(OrchestrationInstanceStatus.DeadLettered, true)]
    [InlineData(OrchestrationInstanceStatus.Compensating, true)]
    [InlineData(OrchestrationInstanceStatus.Running, false)]
    public async Task ReconcileRecoversAnInterruptedFenceWithoutCancellingAResumedInstance(
        OrchestrationInstanceStatus status, bool expectCancellation)
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(status);
        instance.DispatchCancellationPending = true;
        var task = CreateTask(instance.Id, TaskExecutionStatus.Running);
        var attempt = CreateAttempt(task.Id, TaskExecutionStatus.Running);
        var dispatch = CreateDispatch(attempt.Id, "Enqueued");
        fixture.Arrange(instance, [task], [attempt], [dispatch]);
        var instances = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationDispatchCancellationIntentRepository>();
        ((IOrchestrationDispatchCancellationIntentRepository)instances)
            .GetDispatchCancellationPending(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([instance]));
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var leases = Substitute.For<IOrchestrationInstanceCoordinator>();
        leases.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        fixture.CancellationService.CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TaskDispatchCancellationResult.RequestedResult("Cancelled", "Cancelled"));
        var coordinator = new DefaultOrchestrationDispatchCancellationCoordinator(
            fixture.TaskRepository, fixture.AttemptRepository, fixture.DispatchRepository, fixture.TransitionRepository,
            new NoopRuntimeStorageUnitOfWork(), fixture.CancellationService, leases, instances);

        await coordinator.ReconcilePendingAsync(10);

        Assert.False(instance.DispatchCancellationPending);
        Assert.Equal(expectCancellation ? "Cancelled" : "Enqueued", dispatch.DispatchStatus);
        await instances.Received(1).Update(instance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconcileContinuesPastDispatchesWhoseInstancesAreBusy()
    {
        var fixture = new CancellationFixture();
        var busy = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var available = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var busyTask = CreateTask(busy.Id, TaskExecutionStatus.Cancelled);
        var availableTask = CreateTask(available.Id, TaskExecutionStatus.Cancelled);
        var busyAttempt = CreateAttempt(busyTask.Id, TaskExecutionStatus.Cancelled);
        var availableAttempt = CreateAttempt(availableTask.Id, TaskExecutionStatus.Cancelled);
        var busyDispatch = CreateDispatch(busyAttempt.Id, "Cancelled");
        var availableDispatch = CreateDispatch(availableAttempt.Id, "Cancelled");
        busyDispatch.PhysicalCancellationPending = true;
        availableDispatch.PhysicalCancellationPending = true;
        fixture.Arrange(busy, [busyTask], [busyAttempt], [busyDispatch]);
        fixture.Arrange(available, [availableTask], [availableAttempt], [availableDispatch]);
        fixture.CancellationRepository.GetPhysicalCancellationPending(1, 0, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([busyDispatch]));
        fixture.CancellationRepository.GetPhysicalCancellationPending(1, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([availableDispatch]));
        var leases = Substitute.For<IOrchestrationInstanceCoordinator>();
        leases.TryExecuteAsync(busy.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(false);
        leases.TryExecuteAsync(available.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        fixture.CancellationService.CancelAsync(Arg.Any<TaskDispatchCancellationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TaskDispatchCancellationResult.RequestedResult("Cancelled", "Cancelled"));
        var coordinator = new DefaultOrchestrationDispatchCancellationCoordinator(
            fixture.TaskRepository, fixture.AttemptRepository, fixture.DispatchRepository, fixture.TransitionRepository,
            new NoopRuntimeStorageUnitOfWork(), fixture.CancellationService, leases);

        Assert.Equal(1, await coordinator.ReconcilePendingAsync(1));
        Assert.True(busyDispatch.PhysicalCancellationPending);
        Assert.False(availableDispatch.PhysicalCancellationPending);
    }

    [Fact]
    public async Task ReconcileContinuesPastCancellationIntentsWhoseInstancesAreBusy()
    {
        var fixture = new CancellationFixture();
        var busy = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var available = CreateInstance(OrchestrationInstanceStatus.Running);
        busy.DispatchCancellationPending = true;
        available.DispatchCancellationPending = true;
        var instances = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationDispatchCancellationIntentRepository>();
        var intents = (IOrchestrationDispatchCancellationIntentRepository)instances;
        intents.GetDispatchCancellationPending(1, 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([busy]));
        intents.GetDispatchCancellationPending(1, 1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([available]));
        instances.GetById(available.Id, Arg.Any<CancellationToken>()).Returns(available);
        var leases = Substitute.For<IOrchestrationInstanceCoordinator>();
        leases.TryExecuteAsync(busy.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(false);
        leases.TryExecuteAsync(available.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        fixture.CancellationRepository.GetPhysicalCancellationPending(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskDispatch>>([]));
        var coordinator = new DefaultOrchestrationDispatchCancellationCoordinator(
            fixture.TaskRepository, fixture.AttemptRepository, fixture.DispatchRepository, fixture.TransitionRepository,
            new NoopRuntimeStorageUnitOfWork(), fixture.CancellationService, leases, instances);

        await coordinator.ReconcilePendingAsync(1);

        Assert.True(busy.DispatchCancellationPending);
        Assert.False(available.DispatchCancellationPending);
        await instances.Received(1).Update(available, Arg.Any<CancellationToken>());
    }

    private static OrchestrationInstance CreateInstance(OrchestrationInstanceStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "order-flow",
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = status
        };

    private static TaskExecution CreateTask(Id instanceId, TaskExecutionStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = Id.New(),
            TaskKey = "task",
            Status = status
        };

    private static TaskExecutionAttempt CreateAttempt(Id taskId, TaskExecutionStatus status)
        => new()
        {
            Id = Id.New(),
            TaskExecutionId = taskId,
            Status = status
        };

    private static TaskDispatch CreateDispatch(Id attemptId, string status)
        => new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attemptId,
            DispatchType = "Messaging",
            DispatchStatus = status
        };

    private sealed class CancellationFixture
    {
        public CancellationFixture()
        {
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Coordinator = new DefaultOrchestrationDispatchCancellationCoordinator(
                TaskRepository,
                AttemptRepository,
                DispatchRepository,
                TransitionRepository,
                new NoopRuntimeStorageUnitOfWork(),
                CancellationService);
        }

        public ITaskExecutionRepository TaskRepository { get; } = Substitute.For<ITaskExecutionRepository>();

        public ITaskExecutionAttemptRepository AttemptRepository { get; } = Substitute.For<ITaskExecutionAttemptRepository>();

        public ITaskDispatchRepository DispatchRepository { get; } = Substitute.For<ITaskDispatchRepository, ITaskDispatchCancellationRepository>();

        public ITaskDispatchCancellationRepository CancellationRepository => (ITaskDispatchCancellationRepository)DispatchRepository;

        public IExecutionTransitionRepository TransitionRepository { get; } = Substitute.For<IExecutionTransitionRepository>();

        public ITaskDispatchCancellationService CancellationService { get; } = Substitute.For<ITaskDispatchCancellationService>();

        public DefaultOrchestrationDispatchCancellationCoordinator Coordinator { get; }

        public List<ExecutionTransition> Transitions { get; } = [];

        public void Arrange(
            OrchestrationInstance instance,
            IReadOnlyCollection<TaskExecution> tasks,
            IReadOnlyCollection<TaskExecutionAttempt> attempts,
            IReadOnlyCollection<TaskDispatch> dispatches)
        {
            TaskRepository.GetByInstanceId(instance.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(tasks));
            foreach (var task in tasks)
            {
                TaskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(task));
                AttemptRepository
                    .GetByTaskExecutionId(task.Id, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(
                        attempts.Where(attempt => attempt.TaskExecutionId == task.Id).ToArray()));
            }

            foreach (var dispatch in dispatches)
            {
                DispatchRepository.TryGetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(dispatch));
                var attempt = attempts.Single(candidate => candidate.Id == dispatch.TaskExecutionAttemptId);
                AttemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(attempt));
                attempt.DispatchId = dispatch.Id;
            }
        }
    }
}
