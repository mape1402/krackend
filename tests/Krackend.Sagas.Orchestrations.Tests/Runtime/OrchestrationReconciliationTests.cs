namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class OrchestrationReconciliationTests
{
    [Fact]
    public async Task BusyInstanceDoesNotInspectOrModifyWork()
    {
        var f = new Fixture();
        f.Lease.TryExecuteAsync(f.Instance.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(false);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Busy", result.Status);
        await f.Tasks.DidNotReceiveWithAnyArgs().GetByInstanceId(default);
        await f.Transitions.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task ConcurrentRequestDoesNotDuplicateContinuation()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Completed;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owned = 0;
        f.Lease.TryExecuteAsync(f.Instance.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call =>
            {
                if (Interlocked.Exchange(ref owned, 1) != 0) return false;
                try { await call.ArgAt<Func<CancellationToken, Task>>(1)(call.ArgAt<CancellationToken>(2)); return true; }
                finally { Volatile.Write(ref owned, 0); }
            });
        f.Engine.OrchestrateAsync(Arg.Any<ForwardIntent>(), Arg.Any<CancellationToken>())
            .Returns(async _ => { entered.SetResult(); await release.Task; });
        var first = f.Service.ReconcileAsync(f.Instance.Id.ToString());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrent = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        release.SetResult();
        var result = await first;
        Assert.Equal("Busy", concurrent.Status);
        Assert.Equal("Recovered", result.Status);
        await f.Engine.Received(1).OrchestrateAsync(Arg.Any<ForwardIntent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OverdueWaitWithStaleTimerIsProcessedWithoutDispatchReplay()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddSeconds(-1));
        state.Task.TimeoutActionId = Id.New().ToString();
        f.Timeout.ProcessTaskTimeoutAsync(state.Task.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutProcessingResult(true));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Contains(result.Findings, item => item.Status == "Recovered" && item.Message.Contains("Timeout"));
        await f.Timeout.Received(1).ProcessTaskTimeoutAsync(state.Task.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task TimeoutGraceWindowSchedulesItsNextDeadline()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddSeconds(-1));
        var grace = DateTime.UtcNow.AddMinutes(1);
        f.Timeout.ProcessTaskTimeoutAsync(state.Task.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutProcessingResult(true, grace));
        f.Scheduler.UsesDurableScheduling.Returns(true);
        f.Scheduler.ScheduleAsync(Arg.Any<TaskResponseTimeoutScheduleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutScheduleResult("grace", "runtime-timeout"));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Scheduler.Received(1).ScheduleAsync(Arg.Is<TaskResponseTimeoutScheduleRequest>(r => r.DueOnUtc == grace), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunningInstanceWithPendingCancellationDoesNotAdvance()
    {
        var f = new Fixture();
        f.Instance.DispatchCancellationPending = true;
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
        await f.CancellationCoordinator.DidNotReceiveWithAnyArgs().CancelPendingAsync(default!, default!);
        await f.Tasks.DidNotReceiveWithAnyArgs().GetByInstanceId(default);
    }

    [Fact]
    public async Task MissingFutureTimerIsRestoredForSameAttempt()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.TimeoutActionId = "stale";
        f.Inspector.InspectTimeoutAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Missing);
        f.Scheduler.UsesDurableScheduling.Returns(true);
        f.Scheduler.ScheduleAsync(Arg.Any<TaskResponseTimeoutScheduleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutScheduleResult("new", "runtime-timeout"));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Scheduler.Received(1).ScheduleAsync(Arg.Is<TaskResponseTimeoutScheduleRequest>(r =>
            r.TaskExecutionId == state.Task.Id && r.TaskExecutionAttemptId == state.Attempt.Id && r.DispatchId == state.Dispatch.Id), Arg.Any<CancellationToken>());
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task TerminalFutureTimerIsReplacedWithStableRecoveryIdentity()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.TimeoutActionId = Id.New().ToString();
        f.Inspector.InspectTimeoutAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Terminal);
        f.Scheduler.UsesDurableScheduling.Returns(true);
        f.Scheduler.ScheduleAsync(Arg.Any<TaskResponseTimeoutScheduleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutScheduleResult("replacement", "runtime-timeout"));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Scheduler.Received(1).ScheduleAsync(Arg.Is<TaskResponseTimeoutScheduleRequest>(r =>
            r.RecoveryOfActionId == state.Task.TimeoutActionId && r.DueOnUtc == state.Task.ResponseDeadlineUtc), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TerminalTimerWithLostReferenceUsesTheDiscoveredActionIdentity()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        var previousId = Id.New().ToString();
        f.Inspector.InspectTimeoutAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Terminal);
        f.Inspector.FindTimeoutActionIdAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(previousId);
        f.Scheduler.UsesDurableScheduling.Returns(true);
        f.Scheduler.ScheduleAsync(Arg.Any<TaskResponseTimeoutScheduleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TaskResponseTimeoutScheduleResult("replacement", "runtime-timeout"));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Scheduler.Received(1).ScheduleAsync(Arg.Is<TaskResponseTimeoutScheduleRequest>(r => r.RecoveryOfActionId == previousId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownFutureTimerIsNotClaimedToBeHealthy()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.TimeoutActionId = "unverified";
        f.Scheduler.UsesDurableScheduling.Returns(true);
        f.Inspector.InspectTimeoutAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Unknown);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Scheduler.DidNotReceiveWithAnyArgs().ScheduleAsync(default!);
    }

    [Fact]
    public async Task FutureWaitWithExistingTimerIsNotReplayed()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.TimeoutActionId = "timer";
        f.Inspector.InspectTimeoutAsync(state.Task, state.Attempt, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Present);
        f.Scheduler.UsesDurableScheduling.Returns(true);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Waiting", result.Status);
        await f.Scheduler.DidNotReceiveWithAnyArgs().ScheduleAsync(default!);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task CallbackResolvedBeforeLeaseIsReadDoesNotRestoreItsTimer()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        f.Lease.TryExecuteAsync(f.Instance.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call =>
            {
                state.Task.Status = TaskExecutionStatus.Completed;
                state.Attempt.Status = TaskExecutionStatus.Completed;
                state.Dispatch.DispatchStatus = "Completed";
                await call.ArgAt<Func<CancellationToken, Task>>(1)(call.ArgAt<CancellationToken>(2));
                return true;
            });
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Scheduler.DidNotReceiveWithAnyArgs().ScheduleAsync(default!);
        await f.Timeout.DidNotReceiveWithAnyArgs().ProcessTaskTimeoutAsync(default, default);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task UncertainPublishedDispatchRequiresInterventionAndIsNotResent()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Running;
        state.Attempt.Status = TaskExecutionStatus.Running;
        state.Dispatch.DispatchStatus = "Enqueued";
        f.Inspector.InspectDispatchAsync(state.Dispatch, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Missing);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task ActiveDurableDispatchIsReportedAsWaiting()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Running;
        state.Attempt.Status = TaskExecutionStatus.Running;
        state.Dispatch.DispatchStatus = "Enqueued";
        f.Inspector.InspectDispatchAsync(state.Dispatch, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Present);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Waiting", result.Status);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task InconsistentWaitRequiresIntervention()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Attempt.Status = TaskExecutionStatus.Completed;
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Scheduler.DidNotReceiveWithAnyArgs().ScheduleAsync(default!);
        await f.Timeout.DidNotReceiveWithAnyArgs().ProcessTaskTimeoutAsync(default, default);
    }

    [Fact]
    public async Task MissingWaitTimestampWithoutConfirmedSendIsNotInvented()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddSeconds(-1));
        state.Task.WaitingSinceUtc = null;
        state.Dispatch.SentOnUtc = null;
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        Assert.Null(state.Task.WaitingSinceUtc);
        await f.Timeout.DidNotReceiveWithAnyArgs().ProcessTaskTimeoutAsync(default, default);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task PendingTaskWithExistingAttemptDoesNotAdvanceOrRepublish()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Pending;
        state.Attempt.Status = TaskExecutionStatus.Running;
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task DuplicateStagesWithoutTasksRequireIntervention()
    {
        var f = new Fixture();
        f.Stages.GetByInstanceId(f.Instance.Id, Arg.Any<CancellationToken>()).Returns([
            new StageExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, StageKey = "stage" },
            new StageExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, StageKey = "STAGE" }
        ]);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task TaskFromAnotherStageIsNotRedispatched()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Running;
        state.Attempt.Status = TaskExecutionStatus.Running;
        state.Dispatch.DispatchStatus = "QueuePending";
        f.Instance.CurrentStageKey = "different-stage";
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task QueuePendingThatProviderCannotConfirmIsReportedAsUncertain()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Running;
        state.Attempt.Status = TaskExecutionStatus.Running;
        state.Dispatch.DispatchStatus = "QueuePending";
        f.Inspector.InspectDispatchAsync(state.Dispatch, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Present);
        f.DispatchRecovery.RecoverDispatchAsync(state.Dispatch.Id, Arg.Any<CancellationToken>()).Returns(true);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        Assert.Contains(result.Findings, x => x.Message.Contains("uncertain"));
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task QueuePendingWithoutDurableEvidenceIsNotRepublished()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Running;
        state.Attempt.Status = TaskExecutionStatus.Running;
        state.Dispatch.DispatchStatus = "QueuePending";
        f.Inspector.InspectDispatchAsync(state.Dispatch, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Missing);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.DispatchRecovery.DidNotReceiveWithAnyArgs().RecoverDispatchAsync(default);
    }

    [Fact]
    public async Task DeferredPhysicalCancellationIsNotReportedAsRecovered()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Dispatch.PhysicalCancellationPending = true;
        f.Cancellations.ReconcileAsync(state.Dispatch.Id, Arg.Any<CancellationToken>()).Returns(true);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Contains(result.Findings, x => x.Status == "Intervention" && x.Message.Contains("Cancellation"));
    }

    [Fact]
    public async Task CompletedTaskCanResumeEngineWithoutCreatingAnAttempt()
    {
        var f = new Fixture();
        var state = f.ArrangeWait(DateTime.UtcNow.AddMinutes(1));
        state.Task.Status = TaskExecutionStatus.Completed;
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Engine.Received(1).OrchestrateAsync(Arg.Is<ForwardIntent>(x => x.MessageMetadata.OrchestrationInstanceId == f.Instance.Id.ToString()), Arg.Any<CancellationToken>());
        await f.Attempts.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task RecoveredCompensationResumesTheCompensationEngine()
    {
        var f = new Fixture();
        f.Instance.Status = OrchestrationInstanceStatus.Compensating;
        var compensation = new CompensationExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, CompensationTaskKey = "undo", Status = "Pending" };
        f.Compensations.GetByInstanceId(f.Instance.Id, Arg.Any<CancellationToken>()).Returns([compensation]);
        f.Compensations.TryGetById(compensation.Id, Arg.Any<CancellationToken>()).Returns(compensation);
        f.Inspector.InspectCompensationAsync(compensation, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Present);
        f.CompensationRecovery.RecoverCompensationAsync(compensation.Id, Arg.Any<CancellationToken>())
            .Returns(_ => { compensation.Status = "Completed"; return true; });
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Recovered", result.Status);
        await f.Engine.Received(1).OrchestrateAsync(Arg.Is<ForwardIntent>(x => x.MessageMetadata.OrchestrationInstanceId == f.Instance.Id.ToString()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UncertainCompensationIsNotRepublished()
    {
        var f = new Fixture();
        f.Instance.Status = OrchestrationInstanceStatus.Compensating;
        var compensation = new CompensationExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, CompensationTaskKey = "undo", Status = "Pending" };
        f.Compensations.GetByInstanceId(f.Instance.Id, Arg.Any<CancellationToken>()).Returns([compensation]);
        f.Inspector.InspectCompensationAsync(compensation, Arg.Any<CancellationToken>()).Returns(DurableWorkPresence.Missing);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.CompensationRecovery.DidNotReceiveWithAnyArgs().RecoverCompensationAsync(default);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task ActiveCompensationWithUnknownStatusDoesNotResumeEngine()
    {
        var f = new Fixture();
        f.Instance.Status = OrchestrationInstanceStatus.Compensating;
        var compensation = new CompensationExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, CompensationTaskKey = "undo", Status = "Running" };
        f.Compensations.GetByInstanceId(f.Instance.Id, Arg.Any<CancellationToken>()).Returns([compensation]);
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    [Fact]
    public async Task FailedCompensationInspectionIsReportedWithoutResumingEngine()
    {
        var f = new Fixture();
        f.Instance.Status = OrchestrationInstanceStatus.Compensating;
        var compensation = new CompensationExecution { Id = Id.New(), OrchestrationInstanceId = f.Instance.Id, CompensationTaskKey = "undo", Status = "Pending" };
        f.Compensations.GetByInstanceId(f.Instance.Id, Arg.Any<CancellationToken>()).Returns([compensation]);
        f.Inspector.InspectCompensationAsync(compensation, Arg.Any<CancellationToken>()).Returns(Task.FromException<DurableWorkPresence>(new InvalidOperationException("storage unavailable")));
        var result = await f.Service.ReconcileAsync(f.Instance.Id.ToString());
        Assert.Equal("Intervention", result.Status);
        Assert.Contains(result.Findings, x => x.Message.Contains("could not be reconciled"));
        await f.Engine.DidNotReceiveWithAnyArgs().OrchestrateAsync(default!, default);
    }

    private sealed class Fixture
    {
        public readonly IOrchestrationInstanceRepository Instances = Substitute.For<IOrchestrationInstanceRepository>();
        public readonly IStageExecutionRepository Stages = Substitute.For<IStageExecutionRepository>();
        public readonly ITaskExecutionRepository Tasks = Substitute.For<ITaskExecutionRepository>();
        public readonly ITaskExecutionAttemptRepository Attempts = Substitute.For<ITaskExecutionAttemptRepository>();
        public readonly ITaskDispatchRepository Dispatches = Substitute.For<ITaskDispatchRepository>();
        public readonly ICompensationExecutionRepository Compensations = Substitute.For<ICompensationExecutionRepository>();
        public readonly IExecutionTransitionRepository Transitions = Substitute.For<IExecutionTransitionRepository>();
        public readonly IOrchestrationInstanceCoordinator Lease = Substitute.For<IOrchestrationInstanceCoordinator>();
        public readonly IOrchestrationDispatchRecoveryCoordinator DispatchRecovery = Substitute.For<IOrchestrationDispatchRecoveryCoordinator>();
        public readonly IOrchestrationDispatchCancellationCoordinator CancellationCoordinator = Substitute.For<IOrchestrationDispatchCancellationCoordinator>();
        public readonly IOrchestrationDispatchCancellationRecoveryCoordinator Cancellations = Substitute.For<IOrchestrationDispatchCancellationRecoveryCoordinator>();
        public readonly IOrchestrationCompensationRecoveryCoordinator CompensationRecovery = Substitute.For<IOrchestrationCompensationRecoveryCoordinator>();
        public readonly ITaskResponseTimeoutDeadlineResolver Deadline = Substitute.For<ITaskResponseTimeoutDeadlineResolver>();
        public readonly ITaskResponseTimeoutScheduler Scheduler = Substitute.For<ITaskResponseTimeoutScheduler>();
        public readonly IOrchestrationTimeoutProcessor Timeout = Substitute.For<IOrchestrationTimeoutProcessor>();
        public readonly IOrchestrationDurableWorkInspector Inspector = Substitute.For<IOrchestrationDurableWorkInspector>();
        public readonly ISagaEngine Engine = Substitute.For<ISagaEngine>();
        public readonly OrchestrationInstance Instance = new() { Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), OrchestrationDefinitionKey = "test", CorrelationId = "test", SagaId = "test", ExecutionKey = "test", CurrentStageKey = "stage", Status = OrchestrationInstanceStatus.Running };
        public readonly DefaultOrchestrationReconciliationService Service;
        public Fixture()
        {
            Instances.GetById(Instance.Id, Arg.Any<CancellationToken>()).Returns(Instance);
            Lease.TryExecuteAsync(Instance.Id, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
                .Returns(async call => { await call.ArgAt<Func<CancellationToken, Task>>(1)(call.ArgAt<CancellationToken>(2)); return true; });
            Service = new(Instances, Stages, Tasks, Attempts, Dispatches, new Fakes.NoopRuntimeStorageUnitOfWork(), Compensations, Transitions, Lease, DispatchRecovery, CancellationCoordinator, Cancellations, CompensationRecovery, Deadline, Scheduler, Timeout, Inspector, Engine, NullLogger<DefaultOrchestrationReconciliationService>.Instance);
        }
        public (TaskExecution Task, TaskExecutionAttempt Attempt, TaskDispatch Dispatch) ArrangeWait(DateTime due)
        {
            var stage = new StageExecution { Id = Id.New(), OrchestrationInstanceId = Instance.Id, StageKey = "stage", Status = StageExecutionStatus.Running };
            Stages.GetByInstanceId(Instance.Id, Arg.Any<CancellationToken>()).Returns([stage]);
            var task = new TaskExecution { Id = Id.New(), OrchestrationInstanceId = Instance.Id, StageExecutionId = stage.Id, TaskKey = "task", Status = TaskExecutionStatus.WaitingResponse, WaitingSinceUtc = due.AddMinutes(-1), ResponseDeadlineUtc = due };
            var attempt = new TaskExecutionAttempt { Id = Id.New(), TaskExecutionId = task.Id, AttemptNumber = 1, Status = TaskExecutionStatus.WaitingResponse };
            var dispatch = new TaskDispatch { Id = Id.New(), TaskExecutionAttemptId = attempt.Id, DispatchType = "Messaging", DispatchStatus = "WaitingResponse", SentOnUtc = task.WaitingSinceUtc };
            attempt.DispatchId = dispatch.Id;
            Tasks.GetByInstanceId(Instance.Id, Arg.Any<CancellationToken>()).Returns([task]);
            Tasks.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
            Attempts.GetByTaskExecutionId(task.Id, Arg.Any<CancellationToken>()).Returns([attempt]);
            Dispatches.TryGetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
            return (task, attempt, dispatch);
        }
    }
}
