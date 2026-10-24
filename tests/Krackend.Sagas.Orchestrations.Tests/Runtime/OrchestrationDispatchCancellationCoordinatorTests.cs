namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using NSubstitute;

public sealed class OrchestrationDispatchCancellationCoordinatorTests
{
    [Fact]
    public async Task CancelPendingCancelsOnlyDurableDispatchesThatHaveNotBeenPublished()
    {
        var fixture = new CancellationFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var pendingTask = CreateTask(instance.Id, TaskExecutionStatus.Running);
        var waitingTask = CreateTask(instance.Id, TaskExecutionStatus.WaitingResponse);
        var pendingAttempt = CreateAttempt(pendingTask.Id, TaskExecutionStatus.Running);
        var waitingAttempt = CreateAttempt(waitingTask.Id, TaskExecutionStatus.WaitingResponse);
        var pendingDispatch = CreateDispatch(pendingAttempt.Id, "RetryPending");
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

        Assert.Equal(1, cancelled);
        Assert.Equal("Cancelled", pendingDispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.Cancelled, pendingAttempt.Status);
        Assert.Equal(TaskExecutionStatus.Cancelled, pendingTask.Status);
        Assert.Equal("operator aborted the orchestration", pendingDispatch.FailureReason);
        Assert.Equal("Requested", pendingDispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Equal("WaitingResponse", waitingDispatch.DispatchStatus);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, waitingAttempt.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, waitingTask.Status);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("TaskDispatchCancelled", transition.TransitionType);
        Assert.Equal("RetryPending", transition.FromStatus);
        await fixture.CancellationService.Received(1).CancelAsync(
            Arg.Is<TaskDispatchCancellationRequest>(request => request.Dispatch == pendingDispatch),
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
        Assert.Equal("ProviderError", dispatch.Metadata["Recovery.DispatchCancellationProviderStatus"]!.GetValue<string>());
        Assert.Equal("provider unavailable", dispatch.Metadata["Recovery.DispatchCancellationProviderMessage"]!.GetValue<string>());
    }

    [Fact]
    public async Task CancelPendingDoesNotRewriteCompletedOrTimedOutDeliveries()
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

        var cancelled = await fixture.Coordinator.CancelPendingAsync(instance, "instance completed");

        Assert.Equal(0, cancelled);
        Assert.Equal("Completed", completedDispatch.DispatchStatus);
        Assert.Equal("TimedOut", timedOutDispatch.DispatchStatus);
        Assert.Empty(fixture.Transitions);
        await fixture.CancellationService.DidNotReceiveWithAnyArgs().CancelAsync(default!, default);
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

        public ITaskDispatchRepository DispatchRepository { get; } = Substitute.For<ITaskDispatchRepository>();

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
                AttemptRepository
                    .GetByTaskExecutionId(task.Id, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(
                        attempts.Where(attempt => attempt.TaskExecutionId == task.Id).ToArray()));
            }

            foreach (var dispatch in dispatches)
            {
                DispatchRepository.TryGetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(dispatch));
                var attempt = attempts.Single(candidate => candidate.Id == dispatch.TaskExecutionAttemptId);
                attempt.DispatchId = dispatch.Id;
            }
        }
    }
}
