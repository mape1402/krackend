using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mule;
using Mule.Dispatching;
using NSubstitute;
using MuleUlid = Mule.Ulid;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class TaskResponseTimeoutSchedulingTests
{
    [Fact]
    public async Task ScheduleAsyncPersistsTimeoutInTheDedicatedLaneAndCapturesItsReference()
    {
        var fixture = CreateFixture();
        DurableAction? stored = null;
        fixture.Storage.AddAsync(Arg.Do<DurableAction>(action => stored = action), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        fixture.Storage.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        fixture.Storage.FindByDeduplicationKeyAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<MuleUlid?>(stored!.Id));
        fixture.Serializer.Serialize(Arg.Any<TaskResponseTimeoutActionRequest>()).Returns("{\"timeout\":true}");

        var dueOnUtc = DateTime.UtcNow.AddHours(1);
        fixture.Task.ResponseDeadlineUtc = dueOnUtc;
        var result = await fixture.Scheduler.ScheduleAsync(CreateRequest(fixture.Task, fixture.Attempt, fixture.Dispatch, dueOnUtc));

        Assert.True(fixture.Scheduler.UsesDurableScheduling);
        Assert.NotNull(stored);
        Assert.Equal("TaskResponseTimeout", stored!.Key.ToString());
        Assert.Equal("runtime-timeout", stored.Lane);
        Assert.Equal("{\"timeout\":true}", stored.Payload);
        Assert.Equal(fixture.Task.OrchestrationInstanceId.ToString(), stored.CorrelationId);
        Assert.Contains(fixture.Task.Id.ToString(), stored.DeduplicationKey, StringComparison.Ordinal);
        Assert.Equal(dueOnUtc, stored.NextAttemptOnUtc!.Value.UtcDateTime);
        Assert.Equal(stored.Id.ToString(), result.ActionId);
        Assert.Equal("runtime-timeout", result.Lane);
        Assert.Equal(stored.Id.ToString(), fixture.Task.TimeoutActionId);
        Assert.Equal("runtime-timeout", fixture.Task.TimeoutActionLane);
        await fixture.Tasks.Received(1).Update(fixture.Task, Arg.Any<CancellationToken>());
        await fixture.Notifier.Received(1).NotifySavedAsync(stored.Id, "runtime-timeout", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncCancelsOnlyAValidPersistedMuleReference()
    {
        var fixture = CreateFixture();
        var actionId = MuleUlid.NewUlid();
        fixture.Cancellation.CancelAsync(actionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(new MuleCancellationResult(actionId, MuleCancellationOutcome.Cancelled, null)));

        await fixture.Scheduler.CancelAsync(new TaskResponseTimeoutCancellationRequest
        {
            OrchestrationInstanceId = fixture.Task.OrchestrationInstanceId,
            TaskExecutionId = fixture.Task.Id,
            ActionId = actionId.ToString(),
            Lane = "runtime-timeout"
        });
        await fixture.Scheduler.CancelAsync(new TaskResponseTimeoutCancellationRequest
        {
            OrchestrationInstanceId = fixture.Task.OrchestrationInstanceId,
            TaskExecutionId = fixture.Task.Id,
            ActionId = "not-an-ulid",
            Lane = "runtime-timeout"
        });

        await fixture.Cancellation.Received(1).CancelAsync(actionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncDoesNotTurnAnAlreadyPersistedCallbackIntoAFailureWhenMuleIsUnavailable()
    {
        var fixture = CreateFixture();
        var actionId = MuleUlid.NewUlid();
        fixture.Cancellation.CancelAsync(actionId, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MuleCancellationResult>(Task.FromException<MuleCancellationResult>(
                new InvalidOperationException("Mule is temporarily unavailable."))));

        await fixture.Scheduler.CancelAsync(new TaskResponseTimeoutCancellationRequest
        {
            OrchestrationInstanceId = fixture.Task.OrchestrationInstanceId,
            TaskExecutionId = fixture.Task.Id,
            ActionId = actionId.ToString(),
            Lane = "runtime-timeout"
        });

        await fixture.Cancellation.Received(1).CancelAsync(actionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsyncPropagatesTheCallerCancellation()
    {
        var fixture = CreateFixture();
        var actionId = MuleUlid.NewUlid();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        fixture.Cancellation.CancelAsync(actionId, cancellationSource.Token)
            .Returns(new ValueTask<MuleCancellationResult>(Task.FromCanceled<MuleCancellationResult>(cancellationSource.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Scheduler.CancelAsync(
            new TaskResponseTimeoutCancellationRequest
            {
                OrchestrationInstanceId = fixture.Task.OrchestrationInstanceId,
                TaskExecutionId = fixture.Task.Id,
                ActionId = actionId.ToString(),
                Lane = "runtime-timeout"
            },
            cancellationSource.Token));
    }

    [Fact]
    public async Task ScheduleAsyncLeavesTheTimerUnreferencedWhenTheCallbackWonTheRace()
    {
        var fixture = CreateFixture();
        DurableAction? stored = null;
        fixture.Storage.AddAsync(Arg.Do<DurableAction>(action => stored = action), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        fixture.Storage.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        fixture.Storage.FindByDeduplicationKeyAsync(Arg.Any<ActionKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<MuleUlid?>(stored!.Id));
        fixture.Serializer.Serialize(Arg.Any<TaskResponseTimeoutActionRequest>()).Returns("{}");
        fixture.Task.Status = TaskExecutionStatus.Completed;

        await fixture.Scheduler.ScheduleAsync(CreateRequest(fixture.Task, fixture.Attempt, fixture.Dispatch, fixture.Task.ResponseDeadlineUtc!.Value));

        Assert.Null(fixture.Task.TimeoutActionId);
        await fixture.Tasks.DidNotReceive().Update(Arg.Any<TaskExecution>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepairAsyncSchedulesOnlyAwaitedTasksMissingTheirTimerReference()
    {
        var fixture = CreateFixture();
        DurableAction? stored = null;
        fixture.Storage.AddAsync(Arg.Do<DurableAction>(action => stored = action), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        fixture.Storage.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        fixture.Storage.FindByDeduplicationKeyAsync(
                Arg.Any<ActionKey>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<MuleUlid?>(stored!.Id));
        fixture.Serializer.Serialize(Arg.Any<TaskResponseTimeoutActionRequest>()).Returns("{}");
        fixture.Tasks.GetResponseTimeoutsMissingAction(25, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([fixture.Task]));
        fixture.Attempts.GetByTaskExecutionId(fixture.Task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([fixture.Attempt]));
        fixture.Dispatches.TryGetById(fixture.Dispatch.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TaskDispatch?>(fixture.Dispatch));

        var repaired = await fixture.Scheduler.RepairAsync(25);

        Assert.Equal(1, repaired);
        Assert.NotNull(stored);
        Assert.Equal(stored!.Id.ToString(), fixture.Task.TimeoutActionId);
        await fixture.Tasks.Received(1).GetResponseTimeoutsMissingAction(25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepairAsyncIgnoresRecordsThatNoLongerRepresentAnAwaitedDispatch()
    {
        var fixture = CreateFixture();
        fixture.Tasks.GetResponseTimeoutsMissingAction(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([fixture.Task]));
        fixture.Attempts.GetByTaskExecutionId(fixture.Task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(
            [new TaskExecutionAttempt
            {
                Id = fixture.Attempt.Id,
                TaskExecutionId = fixture.Task.Id,
                AttemptNumber = fixture.Attempt.AttemptNumber,
                Status = TaskExecutionStatus.Completed,
                DispatchId = fixture.Dispatch.Id
            }]));

        var repaired = await fixture.Scheduler.RepairAsync(10);

        Assert.Equal(0, repaired);
        await fixture.Storage.DidNotReceive().AddAsync(Arg.Any<DurableAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepairAsyncIgnoresAnAttemptWhoseDispatchIsNoLongerWaitingForAResponse()
    {
        var fixture = CreateFixture();
        fixture.Tasks.GetResponseTimeoutsMissingAction(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([fixture.Task]));
        fixture.Attempts.GetByTaskExecutionId(fixture.Task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([fixture.Attempt]));
        fixture.Dispatches.TryGetById(fixture.Dispatch.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TaskDispatch?>(new TaskDispatch
            {
                Id = fixture.Dispatch.Id,
                TaskExecutionAttemptId = fixture.Attempt.Id,
                DispatchType = "Messaging",
                DispatchStatus = "Acknowledged"
            }));

        var repaired = await fixture.Scheduler.RepairAsync(10);

        Assert.Equal(0, repaired);
        await fixture.Storage.DidNotReceive().AddAsync(Arg.Any<DurableAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepairAsyncSkipsAnIndividualTransientFailureAndContinuesTheBoundedRepairPass()
    {
        var fixture = CreateFixture();
        fixture.Tasks.GetResponseTimeoutsMissingAction(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([fixture.Task]));
        fixture.Attempts.GetByTaskExecutionId(fixture.Task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyCollection<TaskExecutionAttempt>>(
                new InvalidOperationException("The attempt store is temporarily unavailable.")));

        var repaired = await fixture.Scheduler.RepairAsync(10);

        Assert.Equal(0, repaired);
        await fixture.Storage.DidNotReceive().AddAsync(Arg.Any<DurableAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepairAsyncPropagatesTheCallerCancellationInsteadOfConvertingItToARepairFailure()
    {
        var fixture = CreateFixture();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        fixture.Tasks.GetResponseTimeoutsMissingAction(10, cancellationSource.Token)
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([fixture.Task]));
        fixture.Attempts.GetByTaskExecutionId(fixture.Task.Id, cancellationSource.Token)
            .Returns(Task.FromCanceled<IReadOnlyCollection<TaskExecutionAttempt>>(cancellationSource.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Scheduler.RepairAsync(10, cancellationSource.Token));
    }

    [Fact]
    public async Task TimeoutActionReschedulesWhenTheTimeoutPolicyGrantsAnAdditionalWaitWindow()
    {
        var instanceId = Id.New();
        var taskId = Id.New();
        var attemptId = Id.New();
        var dispatchId = Id.New();
        var nextDeadline = DateTime.UtcNow.AddMinutes(3);
        var processor = Substitute.For<IOrchestrationTimeoutProcessor>();
        processor.ProcessTaskTimeoutAsync(taskId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TaskResponseTimeoutProcessingResult(true, nextDeadline)));
        var scheduler = Substitute.For<ITaskResponseTimeoutScheduler>();
        scheduler.ScheduleAsync(Arg.Any<TaskResponseTimeoutScheduleRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TaskResponseTimeoutScheduleResult("timer", "runtime-timeout")));
        var action = new TaskResponseTimeoutAction(processor, scheduler);
        var context = new MuleActionContext<TaskResponseTimeoutActionRequest>(
            new DurableAction { Key = ActionKey.From("TaskResponseTimeout"), Lane = "runtime-timeout", Status = DurableActionStatus.Pending },
            new ServiceCollection().BuildServiceProvider(),
            new TaskResponseTimeoutActionRequest
            {
                OrchestrationInstanceId = instanceId.ToString(),
                TaskExecutionId = taskId.ToString(),
                TaskExecutionAttemptId = attemptId.ToString(),
                DispatchId = dispatchId.ToString(),
                AttemptNumber = 2,
                DueOnUtc = DateTime.UtcNow
            });

        await action.ExecuteAsync(context, CancellationToken.None);

        await scheduler.Received(1).ScheduleAsync(
            Arg.Is<TaskResponseTimeoutScheduleRequest>(request =>
                request.OrchestrationInstanceId == instanceId &&
                request.TaskExecutionId == taskId &&
                request.TaskExecutionAttemptId == attemptId &&
                request.DispatchId == dispatchId &&
                request.AttemptNumber == 2 &&
                request.DueOnUtc == nextDeadline),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TimeoutActionDoesNotScheduleAgainWhenTheTimeoutIsTerminalOrStale()
    {
        var processor = Substitute.For<IOrchestrationTimeoutProcessor>();
        var scheduler = Substitute.For<ITaskResponseTimeoutScheduler>();
        var taskId = Id.New();
        processor.ProcessTaskTimeoutAsync(taskId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskResponseTimeoutProcessingResult.Ignored));
        var action = new TaskResponseTimeoutAction(processor, scheduler);
        var context = new MuleActionContext<TaskResponseTimeoutActionRequest>(
            new DurableAction { Key = ActionKey.From("TaskResponseTimeout"), Lane = "runtime-timeout", Status = DurableActionStatus.Pending },
            new ServiceCollection().BuildServiceProvider(),
            new TaskResponseTimeoutActionRequest
            {
                OrchestrationInstanceId = Id.New().ToString(),
                TaskExecutionId = taskId.ToString(),
                TaskExecutionAttemptId = Id.New().ToString(),
                DispatchId = Id.New().ToString(),
                AttemptNumber = 1,
                DueOnUtc = DateTime.UtcNow
            });

        await action.ExecuteAsync(context, CancellationToken.None);

        await scheduler.DidNotReceive().ScheduleAsync(
            Arg.Any<TaskResponseTimeoutScheduleRequest>(),
            Arg.Any<CancellationToken>());
    }

    private static TaskResponseTimeoutScheduleRequest CreateRequest(
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        DateTime dueOnUtc)
        => new()
        {
            OrchestrationInstanceId = task.OrchestrationInstanceId,
            TaskExecutionId = task.Id,
            TaskExecutionAttemptId = attempt.Id,
            DispatchId = dispatch.Id,
            AttemptNumber = attempt.AttemptNumber,
            DueOnUtc = dueOnUtc
        };

    private static TimeoutSchedulerFixture CreateFixture()
    {
        var instanceId = Id.New();
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = Id.New(),
            TaskKey = "payments.authorize",
            Status = TaskExecutionStatus.WaitingResponse,
            ResponseDeadlineUtc = DateTime.UtcNow.AddHours(1)
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "WaitingResponse"
        };
        attempt.DispatchId = dispatch.Id;

        var storage = Substitute.For<IMuleStorage>();
        var serializer = Substitute.For<IMuleSerializer>();
        var notifier = Substitute.For<IMuleCommitNotifier>();
        var cancellation = Substitute.For<IMuleCancellationService>();
        var tasks = Substitute.For<ITaskExecutionRepository>();
        var attempts = Substitute.For<ITaskExecutionAttemptRepository>();
        var dispatches = Substitute.For<ITaskDispatchRepository>();
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(
                Arg.Any<Id>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>())
            .Returns(call => ExecuteUnderLeaseAsync(call.ArgAt<Func<CancellationToken, Task>>(1)));
        tasks.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(task));
        tasks.Update(Arg.Any<TaskExecution>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddKrackendOrchestrationsRuntime().AddMule(_ => { });
        services.Replace(ServiceDescriptor.Scoped(_ => storage));
        services.Replace(ServiceDescriptor.Scoped(_ => serializer));
        services.Replace(ServiceDescriptor.Scoped(_ => notifier));
        services.Replace(ServiceDescriptor.Scoped(_ => cancellation));
        services.Replace(ServiceDescriptor.Scoped(_ => tasks));
        services.Replace(ServiceDescriptor.Scoped(_ => attempts));
        services.Replace(ServiceDescriptor.Scoped(_ => dispatches));
        services.Replace(ServiceDescriptor.Scoped(_ => coordinator));

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new TimeoutSchedulerFixture(
            provider,
            scope,
            scope.ServiceProvider.GetRequiredService<ITaskResponseTimeoutScheduler>(),
            storage,
            serializer,
            notifier,
            cancellation,
            tasks,
            attempts,
            dispatches,
            task,
            attempt,
            dispatch);
    }

    private static async Task<bool> ExecuteUnderLeaseAsync(Func<CancellationToken, Task> operation)
    {
        await operation(CancellationToken.None);
        return true;
    }

    private sealed class TimeoutSchedulerFixture(
        ServiceProvider provider,
        IServiceScope scope,
        ITaskResponseTimeoutScheduler scheduler,
        IMuleStorage storage,
        IMuleSerializer serializer,
        IMuleCommitNotifier notifier,
        IMuleCancellationService cancellation,
        ITaskExecutionRepository tasks,
        ITaskExecutionAttemptRepository attempts,
        ITaskDispatchRepository dispatches,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch) : IDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public IServiceScope Scope { get; } = scope;
        public ITaskResponseTimeoutScheduler Scheduler { get; } = scheduler;
        public IMuleStorage Storage { get; } = storage;
        public IMuleSerializer Serializer { get; } = serializer;
        public IMuleCommitNotifier Notifier { get; } = notifier;
        public IMuleCancellationService Cancellation { get; } = cancellation;
        public ITaskExecutionRepository Tasks { get; } = tasks;
        public ITaskExecutionAttemptRepository Attempts { get; } = attempts;
        public ITaskDispatchRepository Dispatches { get; } = dispatches;
        public TaskExecution Task { get; } = task;
        public TaskExecutionAttempt Attempt { get; } = attempt;
        public TaskDispatch Dispatch { get; } = dispatch;

        public void Dispose()
        {
            Scope.Dispose();
            Provider.Dispose();
        }
    }
}
