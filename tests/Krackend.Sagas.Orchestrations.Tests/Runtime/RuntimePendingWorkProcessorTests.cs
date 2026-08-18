using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimePendingWorkProcessorTests
{
    [Fact]
    public async Task ProcessDueWork_ReturnsWaitingTasksAttemptsAndPendingCompensations()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now, TimeoutScenario.None);
        var compensation = PendingCompensation(store.Instance.Id, store.Task.Id);
        store.Compensations.Add(compensation);
        store.Dispatches.Add(ScheduledDispatch(store.Attempt.Id, now));

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(1, result.WaitingTaskCount);
        Assert.Equal(1, result.WaitingAttemptCount);
        Assert.Equal(1, result.ScheduledDispatchCount);
        Assert.Equal(1, result.PendingCompensationCount);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingTaskTimeout && x.Id == store.Task.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingAttemptTimeout && x.Id == store.Attempt.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.ScheduledDispatch);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.PendingCompensation && x.Id == compensation.Id);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnWaitingItemsNewerThanScanInstant()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(1), TimeoutScenario.None);

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnCompletedResponses()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.Fail);
        store.Task.Status = TaskExecutionStatus.Completed;
        store.Attempt.Status = TaskExecutionStatus.Completed;

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotApplyTimeoutBeforePolicyDurationElapses()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMilliseconds(-100), TimeoutScenario.Fail);

        await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Running, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Waiting, store.Instance.Status);
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "TaskTimedOut");
    }

    [Fact]
    public async Task ProcessDueWork_ResumesTaskWhenAttemptAlreadyCompleted()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.Fail);
        store.Attempt.Status = TaskExecutionStatus.Completed;
        store.Attempt.CompletedOnUtc = now.AddMinutes(-1);
        store.Attempt.DispatchId = Id.New();
        store.Attempt.ResponsePayload = JsonNode.Parse("""{"succeeded":true}""");
        var runtimeEngine = new RecordingRuntimeEngine();

        var result = await CreateProcessor(store, runtimeEngine: runtimeEngine).ProcessDueWork(now);

        Assert.Empty(result.Items);
        var command = Assert.Single(runtimeEngine.ResponseCommands);
        Assert.Equal(store.Instance.Id.ToString(), command.OrchestrationInstanceId);
        Assert.Equal(store.Task.Id.ToString(), command.TaskExecutionId);
        Assert.Equal(store.Attempt.DispatchId.ToString(), command.DispatchId);
        Assert.Equal(store.Task.CorrelationId, command.CorrelationId);
    }

    [Fact]
    public async Task ProcessDueWork_WhenInstanceIsBusy_SkipsResumeWithoutFailingTick()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.Fail);
        store.Attempt.Status = TaskExecutionStatus.Completed;
        store.Attempt.CompletedOnUtc = now.AddMinutes(-1);
        store.Attempt.DispatchId = Id.New();
        store.Attempt.ResponsePayload = JsonNode.Parse("""{"succeeded":true}""");
        var runtimeEngine = new RecordingRuntimeEngine(throwBusy: true);

        var result = await CreateProcessor(store, runtimeEngine: runtimeEngine).ProcessDueWork(now);

        Assert.NotEmpty(result.Items);
        Assert.Empty(runtimeEngine.ResponseCommands);
    }

    [Fact]
    public async Task ProcessDueWork_AppliesFailTimeoutPolicyAndStopsInstance()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.Fail);

        await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.TimedOut, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.Failed, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Failed, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimedOut");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimeoutPolicyApplied");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    [Fact]
    public async Task ProcessDueWork_AppliesTimeoutWhenInstanceIsRunningButTaskIsWaiting()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.Fail);
        store.Instance.Status = OrchestrationInstanceStatus.Running;

        await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.TimedOut, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.Failed, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Failed, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimedOut");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimeoutPolicyApplied");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    [Fact]
    public async Task ProcessDueWork_AppliesWaitBlockTimeoutPolicyWithoutClosingWaitingTask()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.WaitBlock);

        await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Running, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Waiting, store.Instance.Status);
        Assert.Equal("Wait", store.Task.Metadata["timeoutBehavior"]!.GetValue<string>());
        Assert.Equal("Block", store.Task.Metadata["timeoutAction"]!.GetValue<string>());
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimedOut");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimeoutPolicyApplied");
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    [Fact]
    public async Task ProcessDueWork_MarksReconcileTimeoutAsUnsupportedAndKeepsInstanceBlocked()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.ReconcileBlock);

        await CreateProcessor(store, dispatcher: null).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Running, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Waiting, store.Instance.Status);
        Assert.Equal("Reconcile", store.Task.Metadata["timeoutBehavior"]!.GetValue<string>());
        Assert.Equal("Block", store.Task.Metadata["timeoutAction"]!.GetValue<string>());
        Assert.Equal("ReconciliationUnsupported", store.Task.Metadata["timeoutPolicyApplied"]!.GetValue<string>());
        Assert.Equal("Unsupported", store.Task.Metadata["reconciliationStatus"]!.GetValue<string>());
        Assert.Equal("Unsupported", store.Instance.Metadata["reconciliationStatus"]!.GetValue<string>());
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimedOut");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskReconciliationUnsupported");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimeoutPolicyApplied");
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    [Fact]
    public async Task ProcessDueWork_ReconcilesTimedOutMessagingTaskByRedispatching()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.ReconcileBlock);
        store.Attempt.RequestPayload = JsonNode.Parse("""{"orderId":"order-1","reserved":true}""");
        var dispatcher = new RecordingTaskDispatcher();
        var scheduler = new RecordingDurableWorkScheduler();

        await CreateProcessor(store, dispatcher, scheduler).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.TimedOut, store.Attempts[0].Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Attempts[1].Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, store.Task.Status);
        Assert.Equal(OrchestrationInstanceStatus.Waiting, store.Instance.Status);
        Assert.Equal(2, store.Task.LastAttemptNumber);
        Assert.Equal("ReconciliationRedispatched", store.Task.Metadata["timeoutPolicyApplied"]!.GetValue<string>());
        Assert.Equal("Redispatched", store.Instance.Metadata["reconciliationStatus"]!.GetValue<string>());
        Assert.Equal(store.Task.CorrelationId, Assert.Single(store.Dispatches).CorrelationId);
        Assert.Equal(store.Dispatches[0].Id, store.Attempts[1].DispatchId);
        Assert.Equal("inventory.reserve", Assert.Single(scheduler.Dispatches).Destination.Address);
        Assert.Equal(store.Task.CorrelationId, Assert.Single(scheduler.Dispatches).CorrelationId);
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskReconciliationRedispatched");
    }

    [Fact]
    public async Task ProcessDueWork_FailsReconcileTimeoutWhenAttemptsAreExhausted()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.ReconcileNoRetries);
        var dispatcher = new RecordingTaskDispatcher();

        await CreateProcessor(store, dispatcher).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.TimedOut, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.Failed, store.Task.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Equal("ReconciliationExhausted", store.Task.Metadata["timeoutPolicyApplied"]!.GetValue<string>());
        Assert.Empty(store.Dispatches);
        Assert.Empty(dispatcher.Requests);
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskReconciliationExhausted");
    }

    [Fact]
    public async Task ProcessDueWork_ReconcileRetryCanRunUntilRetryPolicyIsExhausted()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.ReconcileBlock);
        var dispatcher = new RecordingTaskDispatcher();
        var scheduler = new RecordingDurableWorkScheduler();
        var processor = CreateProcessor(store, dispatcher, scheduler);

        await processor.ProcessDueWork(now);
        store.Attempt.WaitingSinceUtc = now.AddMinutes(1);
        await processor.ProcessDueWork(now.AddMinutes(6));
        store.Attempt.WaitingSinceUtc = now.AddMinutes(7);
        await processor.ProcessDueWork(now.AddMinutes(12));

        Assert.Equal(3, store.Attempts.Count);
        Assert.True(scheduler.Dispatches.Count >= 2);
        Assert.Equal(2, scheduler.Dispatches.Select(x => x.Attempt).Distinct().Count());
        Assert.Equal(TaskExecutionStatus.Failed, store.Task.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Equal("ReconciliationExhausted", store.Task.Metadata["timeoutPolicyApplied"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReapplyBlockingTimeoutPolicy()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), TimeoutScenario.ReconcileBlock);
        var processor = CreateProcessor(store);

        await processor.ProcessDueWork(now);
        var transitionCount = store.Transitions.Count;
        await processor.ProcessDueWork(now.AddMinutes(1));

        Assert.Equal(transitionCount, store.Transitions.Count);
    }

    private static RuntimePendingWorkProcessor CreateProcessor(
        RuntimePendingWorkStore store,
        IRuntimeTaskDispatcher? dispatcher = null,
        IRuntimeDurableWorkScheduler? durableWorkScheduler = null,
        IRuntimeEngine? runtimeEngine = null)
        => new(
            new TaskRepositoryStub(store),
            new AttemptRepositoryStub(store),
            new DispatchRepositoryStub(store),
            new CompensationRepositoryStub(store),
            new InstanceRepositoryStub(store),
            new StageRepositoryStub(store),
            new ArtifactRepositoryStub(store),
            new TransitionRepositoryStub(store),
            new DispatcherResolverStub(dispatcher),
            new RuntimeTimeoutPolicyEvaluator(),
            new RuntimeErrorPolicyResolver(),
            new NoopCompensationExecutor(),
            new NoopRuntimeReactiveEventPublisher(),
            runtimeEngine ?? new RecordingRuntimeEngine(),
            durableWorkScheduler ?? new RecordingDurableWorkScheduler());

    private static CompensationExecution PendingCompensation(Id instanceId, Id sourceTaskExecutionId)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            SourceTaskExecutionId = sourceTaskExecutionId,
            CompensationTaskKey = "compensate:reserve-stock",
            Status = "Pending",
            StartedOnUtc = DateTime.UtcNow
        };

    private sealed class RuntimePendingWorkStore
    {
        public required OrchestrationInstance Instance { get; set; }
        public required StageExecution Stage { get; set; }
        public required TaskExecution Task { get; set; }
        public required TaskExecutionAttempt Attempt { get; set; }
        public List<TaskExecutionAttempt> Attempts { get; } = new();
        public List<TaskDispatch> Dispatches { get; } = new();
        public required RuntimeOrchestrationArtifact Artifact { get; set; }
        public List<CompensationExecution> Compensations { get; } = new();
        public List<ExecutionTransition> Transitions { get; } = new();

        public static RuntimePendingWorkStore Create(DateTime waitingSinceUtc, TimeoutScenario timeoutScenario)
        {
            var artifactId = Id.New();
            var instanceId = Id.New();
            var stageId = Id.New();
            var taskId = Id.New();
            var attempt = new TaskExecutionAttempt
            {
                Id = Id.New(),
                TaskExecutionId = taskId,
                AttemptNumber = 1,
                Status = TaskExecutionStatus.WaitingResponse,
                WaitingSinceUtc = waitingSinceUtc
            };
            var store = new RuntimePendingWorkStore
            {
                Instance = new OrchestrationInstance
                {
                    Id = instanceId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = "order.fulfillment",
                    RuntimeOrchestrationArtifactId = artifactId,
                    CorrelationId = "order-1",
                    ExecutionKey = "order.fulfillment::order-1",
                    Status = OrchestrationInstanceStatus.Waiting,
                    StartedOnUtc = waitingSinceUtc.AddMinutes(-1),
                    LastUpdatedOnUtc = waitingSinceUtc,
                    WaitingSinceUtc = waitingSinceUtc,
                    SnapshotPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
                },
                Stage = new StageExecution
                {
                    Id = stageId,
                    OrchestrationInstanceId = instanceId,
                    StageKey = "reserve-inventory",
                    Order = 1,
                    Status = StageExecutionStatus.Running,
                    StartedOnUtc = waitingSinceUtc.AddMinutes(-1)
                },
                Task = new TaskExecution
                {
                    Id = taskId,
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskKey = "reserve-stock",
                    TaskKind = TaskKind.Messaging,
                    Status = TaskExecutionStatus.WaitingResponse,
                    AwaitResponse = true,
                    WaitingSinceUtc = waitingSinceUtc,
                    OnErrorPolicy = OnErrorPolicy.Stop,
                    CorrelationId = "order-1:reserve-stock"
                },
                Attempt = attempt,
                Artifact = new RuntimeOrchestrationArtifact
                {
                    Id = artifactId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = "order.fulfillment",
                    ArtifactType = "orchestration",
                    Version = new SemanticVersion(1, 0, 0),
                    ArtifactChecksum = new Checksum("checksum"),
                    ArtifactPayload = BuildArtifactPayload(timeoutScenario),
                    IsActive = true,
                    DeployedOnUtc = waitingSinceUtc,
                    ActivatedOnUtc = waitingSinceUtc
                }
            };
            store.Attempts.Add(attempt);
            return store;
        }

        private static JsonNode BuildArtifactPayload(TimeoutScenario timeoutScenario)
            => JsonNode.Parse($$"""
            {
              "Key": "order.fulfillment",
              "Version": { "Major": 1, "Minor": 0, "Patch": 0 },
              "Stages": [
                {
                  "Key": "reserve-inventory",
                  "Order": 1,
                  "Tasks": [
                    {
                      "Key": "reserve-stock",
                      "Order": 1,
                      "Kind": {{(int)TaskKind.Messaging}},
                      "DispatchType": {{(int)TaskDispatchType.FireAndWaitCallback}},
                      "OnErrorPolicy": {{(int)OnErrorPolicy.Stop}},
                      "IsEnabled": true,
                      "Configuration": { "Topic": "inventory.reserve", "Version": { "Major": 1, "Minor": 0, "Patch": 0 } },
                      "TimeoutPolicy": {{TimeoutPolicyJson(timeoutScenario)}}
                    }
                  ]
                }
              ]
            }
            """)!;

        private static string TimeoutPolicyJson(TimeoutScenario timeoutScenario)
            => timeoutScenario switch
            {
                TimeoutScenario.Fail => """{ "Timeout": { "Value": "00:00:01" }, "TimeoutBehavior": 0, "TimeoutBehaviorPolicy": { "ErrorCode": "ReserveTimeout" } }""",
                TimeoutScenario.WaitBlock => """{ "Timeout": { "Value": "00:00:01" }, "TimeoutBehavior": 1, "TimeoutBehaviorPolicy": { "OrchestrationAction": 0, "WaitingTime": { "Value": "00:00:05" } } }""",
                TimeoutScenario.ReconcileBlock => """{ "Timeout": { "Value": "00:00:01" }, "TimeoutBehavior": 2, "TimeoutBehaviorPolicy": { "OrchestrationAction": 0, "RetryPolicy": { "MaxRetries": 2, "StrategyType": 0 } } }""",
                TimeoutScenario.ReconcileNoRetries => """{ "Timeout": { "Value": "00:00:01" }, "TimeoutBehavior": 2, "TimeoutBehaviorPolicy": { "OrchestrationAction": 0, "RetryPolicy": { "MaxRetries": 0, "StrategyType": 0 } } }""",
                _ => "{}"
            };
    }

    private enum TimeoutScenario
    {
        None,
        Fail,
        WaitBlock,
        ReconcileBlock,
        ReconcileNoRetries
    }

    private static TaskDispatch ScheduledDispatch(Id attemptId, DateTime scheduledOnUtc)
        => new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attemptId,
            DispatchType = "Messaging",
            Destination = "inventory.reserve",
            DispatchStatus = "Scheduled",
            ScheduledOnUtc = scheduledOnUtc,
            CommandId = Id.New().ToString(),
            CorrelationId = "order-1:reserve-stock"
        };

    private sealed class TaskRepositoryStub(RuntimePendingWorkStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            store.Task = taskExecution;
            return Task.CompletedTask;
        }
        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>([store.Task]);
        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(store.Task.Status == TaskExecutionStatus.WaitingResponse &&
                                                                   store.Task.WaitingSinceUtc <= dueBeforeUtc
                ? [store.Task]
                : []);
    }

    private sealed class AttemptRepositoryStub(RuntimePendingWorkStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempt = attempt;
            store.Attempts.Add(attempt);
            return Task.CompletedTask;
        }
        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempt = attempt;
            var index = store.Attempts.FindIndex(x => x.Id == attempt.Id);
            if (index >= 0)
                store.Attempts[index] = attempt;
            return Task.CompletedTask;
        }
        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Attempts.FirstOrDefault(x => x.DispatchId == dispatchId));
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempts.Where(x => x.TaskExecutionId == taskExecutionId).ToArray());
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempts
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
                .ToArray());
    }

    private sealed class DispatchRepositoryStub(RuntimePendingWorkStore store) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            store.Dispatches.Add(dispatch);
            return Task.CompletedTask;
        }

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            var index = store.Dispatches.FindIndex(x => x.Id == dispatch.Id);
            if (index >= 0)
                store.Dispatches[index] = dispatch;
            return Task.CompletedTask;
        }

        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = store.Dispatches.SingleOrDefault(x => x.Id == dispatchId);
            if (dispatch is not null)
            {
                dispatch.DispatchStatus = string.IsNullOrWhiteSpace(status) ? "Dispatched" : status;
                dispatch.SentOnUtc = sentOnUtc;
                dispatch.AcknowledgedOnUtc = sentOnUtc;
                dispatch.FailedOnUtc = null;
                dispatch.FailureReason = null;
                dispatch.Metadata["externalReference"] = externalReference ?? string.Empty;
            }

            return Task.CompletedTask;
        }

        public Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = store.Dispatches.SingleOrDefault(x => x.Id == dispatchId);
            if (dispatch is not null)
            {
                dispatch.DispatchStatus = "Failed";
                dispatch.FailedOnUtc = DateTime.UtcNow;
                dispatch.FailureReason = failureReason;
                dispatch.Metadata["externalReference"] = externalReference ?? string.Empty;
            }

            return Task.CompletedTask;
        }

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches.Single(x => x.Id == dispatchId));

        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches.SingleOrDefault(x => x.Id == dispatchId)!);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches.Single(x => x.CommandId == commandId));

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches.Single(x => x.TaskExecutionAttemptId == taskExecutionAttemptId));

        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(store.Dispatches
                .Where(x => string.Equals(x.DispatchStatus, "Scheduled", StringComparison.OrdinalIgnoreCase) &&
                            x.ScheduledOnUtc <= dueBeforeUtc &&
                            x.SentOnUtc == null &&
                            x.FailedOnUtc == null)
                .ToArray());
    }

    private sealed class CompensationRepositoryStub(RuntimePendingWorkStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            store.Compensations.Add(compensationExecution);
            return Task.CompletedTask;
        }
        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Compensations.FirstOrDefault(x => x.Id == compensationExecutionId)!);
        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(store.Compensations.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());
        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(store.Compensations.Where(x => x.Status == "Pending").ToArray());
    }

    private sealed class InstanceRepositoryStub(RuntimePendingWorkStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instance = instance;
            return Task.CompletedTask;
        }
        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult(store.Instance);
        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class StageRepositoryStub(RuntimePendingWorkStore store) : IStageExecutionRepository
    {
        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            store.Stage = stageExecution;
            return Task.CompletedTask;
        }
        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<StageExecution>>([store.Stage]);
    }

    private sealed class ArtifactRepositoryStub(RuntimePendingWorkStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TransitionRepositoryStub(RuntimePendingWorkStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            store.Transitions.Add(transition);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(store.Transitions);
        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoopCompensationExecutor : IRuntimeCompensationExecutor
    {
        public Task<RuntimeCompensationExecutionResult> Execute(CompensationExecution compensation, CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeCompensationExecutionResult(true, compensation.Status, "Noop"));
    }

    private sealed class RecordingRuntimeEngine(bool throwBusy = false) : IRuntimeEngine
    {
        public List<RuntimeMessageResponseCommand> ResponseCommands { get; } = new();

        public Task<RuntimeEngineProcessResult> Process(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimeEngineProcessResult { Succeeded = true, Status = "Processed", Message = "Processed" });

        public Task<RuntimeEngineProcessResult> ContinueFromResponse(RuntimeMessageResponseCommand command, CancellationToken cancellationToken = default)
        {
            if (throwBusy)
                throw new InvalidOperationException($"Orchestration instance '{command.OrchestrationInstanceId}' is busy processing another response.");

            ResponseCommands.Add(command);
            return Task.FromResult(new RuntimeEngineProcessResult
            {
                Succeeded = true,
                Status = "Recovered",
                Message = "Recovered split response state.",
                InstanceId = command.OrchestrationInstanceId
            });
        }
    }

    private sealed class DispatcherResolverStub(IRuntimeTaskDispatcher? dispatcher) : IRuntimeTaskDispatcherResolver
    {
        public IRuntimeTaskDispatcher? Resolve(string taskKind)
            => dispatcher?.CanDispatch(taskKind) == true ? dispatcher : null;
    }

    private sealed class RecordingTaskDispatcher : IRuntimeTaskDispatcher
    {
        public List<RuntimeTaskDispatchRequest> Requests { get; } = new();

        public bool CanDispatch(string taskKind)
            => string.Equals(taskKind, "Messaging", StringComparison.OrdinalIgnoreCase);

        public Task<RuntimeTaskDispatchResult> Dispatch(RuntimeTaskDispatchRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new RuntimeTaskDispatchResult
            {
                Succeeded = true,
                Status = "Dispatched",
                ExternalReference = request.CommandId
            });
        }
    }

    private sealed class RecordingDurableWorkScheduler : IRuntimeDurableWorkScheduler
    {
        public List<RuntimeIngressEnvelope> Ingresses { get; } = new();

        public List<RuntimeDispatchEnvelope> Dispatches { get; } = new();

        public List<RuntimeReconcileRequest> Reconciles { get; } = new();

        public ValueTask<Guid> ScheduleProcessIngress(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Ingresses.Add(envelope);
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Dispatches.Add(envelope);
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> ScheduleReconcile(RuntimeReconcileRequest request, CancellationToken cancellationToken = default)
        {
            Reconciles.Add(request);
            return ValueTask.FromResult(Guid.NewGuid());
        }
    }
}
