using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeEngineIdempotencyTests
{
    [Fact]
    public async Task ContinueFromResponse_WhenTaskAlreadyCompleted_IgnoresDuplicateWithoutWritingTransitions()
    {
        var store = new RuntimeStore();
        var artifactId = Id.New();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = artifactId,
            CorrelationId = "order-1",
            ExecutionKey = "order.fulfillment::order-1",
            Status = OrchestrationInstanceStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            CompletedOnUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "reserve-inventory",
            Order = 1,
            Status = StageExecutionStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Completed,
            AwaitResponse = true,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow,
            LastAttemptNumber = 1,
            CorrelationId = "order-1:reserve-stock"
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Completed,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            CompletedOnUtc = DateTime.UtcNow,
            DispatchId = Id.New(),
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            ResponsePayload = JsonNode.Parse("""{"reserved":true}""")
        };
        var dispatch = new TaskDispatch
        {
            Id = attempt.DispatchId.Value,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventory.reserve",
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            DispatchStatus = "Dispatched",
            CommandId = Id.New().ToString(),
            CorrelationId = task.CorrelationId,
            SentOnUtc = DateTime.UtcNow.AddSeconds(-30),
            AcknowledgedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        store.Instances[instance.Id] = instance;
        store.Stages[stage.Id] = stage;
        store.Tasks[task.Id] = task;
        store.Attempts[attempt.Id] = attempt;
        store.Dispatches[dispatch.Id] = dispatch;

        var engine = CreateEngine(store);
        var result = await engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = instance.Id.ToString(),
            TaskExecutionId = task.Id.ToString(),
            DispatchId = attempt.DispatchId.Value.ToString(),
            CorrelationId = task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Completed", result.Status);
        Assert.Contains("ignored", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Transitions);
        Assert.Single(store.Stages);
        Assert.Equal(TaskExecutionStatus.Completed, store.Tasks[task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenTaskBelongsToAnotherInstance_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        var foreignInstance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = trace.Instance.EnvironmentKey,
            OrchestrationDefinitionKey = trace.Instance.OrchestrationDefinitionKey,
            RuntimeOrchestrationArtifactId = trace.Instance.RuntimeOrchestrationArtifactId,
            CorrelationId = "order-2",
            ExecutionKey = "order.fulfillment::order-2",
            Status = OrchestrationInstanceStatus.Waiting,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            WaitingSinceUtc = DateTime.UtcNow
        };
        trace.Store.Instances[foreignInstance.Id] = foreignInstance;

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = foreignInstance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not belong to orchestration instance", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenCorrelationDoesNotMatchTask_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        var engine = CreateEngine(trace.Store);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = "order-1:other-task",
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not match task correlation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenCorrelationDoesNotMatchDispatch_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        trace.Store.Dispatches[trace.Dispatch.Id].CorrelationId = "order-1:other-dispatch";

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not match dispatch correlation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    [Fact]
    public async Task ContinueFromResponse_WhenDispatchBelongsToAnotherAttempt_RejectsTraceWithoutWritingTransitions()
    {
        var trace = CreateWaitingTrace();
        trace.Store.Dispatches[trace.Dispatch.Id].TaskExecutionAttemptId = Id.New();

        var engine = CreateEngine(trace.Store);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = trace.Instance.Id.ToString(),
            TaskExecutionId = trace.Task.Id.ToString(),
            DispatchId = trace.Dispatch.Id.ToString(),
            CorrelationId = trace.Task.CorrelationId,
            Payload = JsonNode.Parse("""{"reserved":true}""")
        }));

        Assert.Contains("does not belong to task attempt", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(trace.Store.Transitions);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, trace.Store.Tasks[trace.Task.Id].Status);
    }

    private static RuntimeTraceFixture CreateWaitingTrace()
    {
        var store = new RuntimeStore();
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            RuntimeOrchestrationArtifactId = Id.New(),
            CorrelationId = "order-1",
            ExecutionKey = "order.fulfillment::order-1",
            Status = OrchestrationInstanceStatus.Waiting,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastUpdatedOnUtc = DateTime.UtcNow,
            WaitingSinceUtc = DateTime.UtcNow,
            SnapshotPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "reserve-inventory",
            Order = 1,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.WaitingResponse,
            AwaitResponse = true,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-30),
            LastAttemptNumber = 1,
            CorrelationId = "order-1:reserve-stock"
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            WaitingSinceUtc = DateTime.UtcNow.AddSeconds(-30),
            DispatchId = Id.New(),
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}""")
        };
        var dispatch = new TaskDispatch
        {
            Id = attempt.DispatchId.Value,
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = "inventory.reserve",
            RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
            DispatchStatus = "Dispatched",
            CommandId = Id.New().ToString(),
            CorrelationId = task.CorrelationId,
            SentOnUtc = DateTime.UtcNow.AddSeconds(-30),
            AcknowledgedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        store.Instances[instance.Id] = instance;
        store.Stages[stage.Id] = stage;
        store.Tasks[task.Id] = task;
        store.Attempts[attempt.Id] = attempt;
        store.Dispatches[dispatch.Id] = dispatch;

        return new RuntimeTraceFixture(store, instance, stage, task, attempt, dispatch);
    }

    private sealed record RuntimeTraceFixture(
        RuntimeStore Store,
        OrchestrationInstance Instance,
        StageExecution Stage,
        TaskExecution Task,
        TaskExecutionAttempt Attempt,
        TaskDispatch Dispatch);

    private static RuntimeEngine CreateEngine(RuntimeStore store)
        => new(new RuntimeEngineDependencies
        {
            IntakeBuffer = new ThrowingIntakeBuffer(),
            TriggerPromoter = new ThrowingTriggerPromoter(),
            ArtifactRepository = new RuntimeArtifactRepositoryStub(),
            StageRepository = new StageRepositoryStub(store),
            TaskRepository = new TaskRepositoryStub(store),
            AttemptRepository = new AttemptRepositoryStub(store),
            DispatchRepository = new DispatchRepositoryStub(store),
            InstanceRepository = new InstanceRepositoryStub(store),
            TimelineRepository = new TransitionRepositoryStub(store),
            TaskDispatcherResolver = new ThrowingTaskDispatcherResolver(),
            ConditionEvaluator = new RuntimeConditionEvaluator(),
            ReactiveEventPublisher = new NoopRuntimeReactiveEventPublisher()
        });

    private sealed class RuntimeStore
    {
        public Dictionary<Id, OrchestrationInstance> Instances { get; } = new();
        public Dictionary<Id, StageExecution> Stages { get; } = new();
        public Dictionary<Id, TaskExecution> Tasks { get; } = new();
        public Dictionary<Id, TaskExecutionAttempt> Attempts { get; } = new();
        public Dictionary<Id, TaskDispatch> Dispatches { get; } = new();
        public List<ExecutionTransition> Transitions { get; } = new();
    }

    private sealed class InstanceRepositoryStub(RuntimeStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Instances[instanceId]);

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(store.Instances.Values.ToArray());

        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default)
        {
            var instances = store.Instances.Values.Where(x => x.EnvironmentKey == environmentKey).ToArray();
            return Task.FromResult(new RuntimeInstanceSummary(
                instances.Count(x => x.Status is OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Completed && x.LastUpdatedOnUtc >= recentSinceUtc),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Failed && x.LastUpdatedOnUtc >= recentSinceUtc),
                recentSinceUtc));
        }
    }

    private sealed class StageRepositoryStub(RuntimeStore store) : IStageExecutionRepository
    {
        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Stages[stageExecutionId]);

        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<StageExecution>>(store.Stages.Values.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());
    }

    private sealed class TaskRepositoryStub(RuntimeStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Tasks[taskExecutionId]);

        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must resolve tasks by id.");

        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(store.Tasks.Values.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());
    }

    private sealed class AttemptRepositoryStub(RuntimeStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Attempts[attemptId]);

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempts.Values.Where(x => x.TaskExecutionId == taskExecutionId).ToArray());
    }

    private sealed class DispatchRepositoryStub(RuntimeStore store) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Dispatches[dispatchId]);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must not resolve dispatches by command id.");

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must resolve dispatches by id.");
    }

    private sealed class TransitionRepositoryStub(RuntimeStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            store.Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(store.Transitions.Where(x => x.OrchestrationInstanceId == instanceId).ToArray());

        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(store.Transitions.Take(take).ToArray());

        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>(
                store.Transitions
                    .Where(x => x.OccurredOnUtc >= sinceUtc)
                    .GroupBy(x => new DateTime(x.OccurredOnUtc.Year, x.OccurredOnUtc.Month, x.OccurredOnUtc.Day, x.OccurredOnUtc.Hour, x.OccurredOnUtc.Minute, 0, DateTimeKind.Utc))
                    .Select(x => new RuntimeTrafficPoint(
                        x.Key,
                        x.Count(item => item.TransitionType == "InstanceStarted"),
                        x.Count(item => item.TransitionType == "InstanceCompleted"),
                        x.Count(item => item.TransitionType == "InstanceFailed")))
                    .ToArray());
    }

    private sealed class RuntimeArtifactRepositoryStub : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Duplicate responses must not resolve runtime artifacts.");

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(Array.Empty<RuntimeOrchestrationArtifact>());

        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class ThrowingTaskDispatcherResolver : IRuntimeTaskDispatcherResolver
    {
        public IRuntimeTaskDispatcher Resolve(string taskKind)
            => throw new InvalidOperationException("Duplicate responses must not dispatch tasks.");
    }

    private sealed class ThrowingTriggerPromoter : ITriggerPromoter
    {
        public Task<TriggerPromotionResult> Promote(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class ThrowingIntakeBuffer : ITriggerIntakeBuffer
    {
        public Task<TriggerIntakeBufferResult> Enqueue(TriggerIntakeBufferItem item, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkCompleted(Id bufferItemId, string leaseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TriggerIntakeBufferResult> MarkFailed(Id bufferItemId, string leaseId, string errorMessage, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
