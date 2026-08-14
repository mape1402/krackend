using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeCompensationExecutorTests
{
    [Fact]
    public async Task Execute_DispatchesMessagingCompensationAndMarksInstanceCompensated()
    {
        var store = RuntimeCompensationStore.Create(nameof(TaskDispatchType.FireAndForget));
        var dispatcher = new RecordingDispatcher(new RuntimeTaskDispatchResult
        {
            Succeeded = true,
            Status = "Dispatched",
            ExternalReference = "rabbit:rollback-1"
        });

        var result = await CreateExecutor(store, dispatcher).Execute(store.Compensation);

        Assert.True(result.Succeeded);
        Assert.Equal("Completed", store.Compensation.Status);
        Assert.Equal(OrchestrationInstanceStatus.Compensated, store.Instance.Status);
        Assert.Equal("Compensated", store.Instance.FinalOutcome);
        Assert.Equal("inventory.release", dispatcher.Request!.Destination);
        Assert.Equal("1.0.0", dispatcher.Request.MessageVersion);
        Assert.Equal("compensate:reserve-stock", dispatcher.Request.TaskKey);
        Assert.Equal(store.SourceTask.Id.ToString(), dispatcher.Request.TaskExecutionId);
        Assert.Equal("""{"orderId":"order-1"}""", dispatcher.Request.Payload.ToJsonString());
        Assert.Contains(store.Transitions, x => x.TransitionType == "CompensationStarted");
        Assert.Contains(store.Transitions, x => x.TransitionType == "CompensationDispatched");
        Assert.Contains(store.Transitions, x => x.TransitionType == "CompensationCompleted");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceCompensated");
    }

    [Fact]
    public async Task Execute_MarksCallbackCompensationAsUnsupportedWithoutDispatching()
    {
        var store = RuntimeCompensationStore.Create(nameof(TaskDispatchType.FireAndWaitCallback));
        var dispatcher = new RecordingDispatcher(new RuntimeTaskDispatchResult { Succeeded = true, Status = "Dispatched" });

        var result = await CreateExecutor(store, dispatcher).Execute(store.Compensation);

        Assert.False(result.Succeeded);
        Assert.Equal("Unsupported", store.Compensation.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Null(dispatcher.Request);
        Assert.Contains(store.Transitions, x => x.TransitionType == "CompensationFailed");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    [Fact]
    public async Task Execute_ResumesStartedMessagingCompensationAndMarksInstanceCompensated()
    {
        var store = RuntimeCompensationStore.Create(nameof(TaskDispatchType.FireAndForget), "Started");
        store.Compensation.StartedOnUtc = DateTime.UtcNow.AddMinutes(-5);
        store.Compensation.Metadata["dispatchId"] = "existing-dispatch";
        store.Compensation.Metadata["commandId"] = $"compensation:{store.Compensation.Id}";
        var dispatcher = new RecordingDispatcher(new RuntimeTaskDispatchResult
        {
            Succeeded = true,
            Status = "Dispatched",
            ExternalReference = "rabbit:rollback-resumed"
        });

        var result = await CreateExecutor(store, dispatcher).Execute(store.Compensation);

        Assert.True(result.Succeeded);
        Assert.Equal("Completed", store.Compensation.Status);
        Assert.Equal(OrchestrationInstanceStatus.Compensated, store.Instance.Status);
        Assert.Equal("existing-dispatch", dispatcher.Request!.DispatchId);
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "CompensationStarted");
        Assert.Contains(store.Transitions, x => x.TransitionType == "CompensationCompleted");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceCompensated");
    }

    [Fact]
    public async Task Execute_WhenDispatcherSchedulesDurableWork_KeepsCompensationStarted()
    {
        var store = RuntimeCompensationStore.Create(nameof(TaskDispatchType.FireAndForget));
        var dispatcher = new RecordingDispatcher(new RuntimeTaskDispatchResult
        {
            Succeeded = true,
            Status = "Scheduled",
            ExternalReference = "mule-action-1"
        });

        var result = await CreateExecutor(store, dispatcher).Execute(store.Compensation);

        Assert.True(result.Succeeded);
        Assert.Equal("Started", store.Compensation.Status);
        Assert.Equal(OrchestrationInstanceStatus.Compensating, store.Instance.Status);
        Assert.Equal("Scheduled", store.Compensation.Metadata["dispatchStatus"]!.GetValue<string>());
        Assert.Equal("mule-action-1", store.Compensation.Metadata["externalReference"]!.GetValue<string>());
        Assert.Equal(store.Compensation.Id.ToString(), dispatcher.Request!.Metadata["compensationExecutionId"]!.GetValue<string>());
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "CompensationCompleted");
        Assert.DoesNotContain(store.Transitions, x => x.TransitionType == "InstanceCompensated");
    }

    [Fact]
    public async Task Execute_WhenStartedCompensationIsAlreadyScheduled_DoesNotRedispatch()
    {
        var store = RuntimeCompensationStore.Create(nameof(TaskDispatchType.FireAndForget), "Started");
        store.Compensation.Metadata["dispatchStatus"] = "Scheduled";
        store.Compensation.Metadata["externalReference"] = "mule-action-1";
        var dispatcher = new RecordingDispatcher(new RuntimeTaskDispatchResult { Succeeded = true, Status = "Dispatched" });

        var result = await CreateExecutor(store, dispatcher).Execute(store.Compensation);

        Assert.True(result.Succeeded);
        Assert.Equal("Started", store.Compensation.Status);
        Assert.Null(dispatcher.Request);
    }

    private static RuntimeCompensationExecutor CreateExecutor(RuntimeCompensationStore store, RecordingDispatcher dispatcher)
        => new(
            new CompensationRepositoryStub(store),
            new InstanceRepositoryStub(store),
            new TaskRepositoryStub(store),
            new DispatcherResolverStub(dispatcher),
            new TransitionRepositoryStub(store),
            new NoopRuntimeReactiveEventPublisher());

    private sealed class RuntimeCompensationStore
    {
        public required OrchestrationInstance Instance { get; init; }
        public required TaskExecution SourceTask { get; init; }
        public required CompensationExecution Compensation { get; init; }
        public List<ExecutionTransition> Transitions { get; } = new();

        public static RuntimeCompensationStore Create(string dispatchType, string compensationStatus = "Pending")
        {
            var instanceId = Id.New();
            var sourceTaskId = Id.New();
            return new RuntimeCompensationStore
            {
                Instance = new OrchestrationInstance
                {
                    Id = instanceId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = "order.fulfillment",
                    CorrelationId = "order-1",
                    ExecutionKey = "order.fulfillment::order-1",
                    Status = OrchestrationInstanceStatus.Compensating,
                    StartedOnUtc = DateTime.UtcNow.AddMinutes(-1),
                    LastUpdatedOnUtc = DateTime.UtcNow.AddMinutes(-1)
                },
                SourceTask = new TaskExecution
                {
                    Id = sourceTaskId,
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = Id.New(),
                    TaskKey = "reserve-stock",
                    TaskKind = TaskKind.Messaging,
                    Status = TaskExecutionStatus.Completed,
                    CompletedOnUtc = DateTime.UtcNow.AddSeconds(-30)
                },
                Compensation = new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    SourceTaskExecutionId = sourceTaskId,
                    CompensationTaskKey = "compensate:reserve-stock",
                    Status = compensationStatus,
                    RequestPayload = JsonNode.Parse("""{"orderId":"order-1"}"""),
                    Metadata = new Dictionary<string, JsonNode>
                    {
                        ["sourceTaskKey"] = "reserve-stock",
                        ["sourceStageKey"] = "reserve-inventory",
                        ["compensationKind"] = nameof(TaskKind.Messaging),
                        ["dispatchType"] = dispatchType,
                        ["destination"] = "inventory.release",
                        ["messageVersion"] = "1.0.0",
                        ["orchestrationVersion"] = "1.0.0"
                    }
                }
            };
        }
    }

    private sealed class RecordingDispatcher(RuntimeTaskDispatchResult result) : IRuntimeTaskDispatcher
    {
        public RuntimeTaskDispatchRequest? Request { get; private set; }

        public bool CanDispatch(string taskKind)
            => string.Equals(taskKind, nameof(TaskKind.Messaging), StringComparison.OrdinalIgnoreCase);

        public Task<RuntimeTaskDispatchResult> Dispatch(RuntimeTaskDispatchRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class DispatcherResolverStub(IRuntimeTaskDispatcher dispatcher) : IRuntimeTaskDispatcherResolver
    {
        public IRuntimeTaskDispatcher Resolve(string taskKind)
            => dispatcher.CanDispatch(taskKind) ? dispatcher : null!;
    }

    private sealed class CompensationRepositoryStub(RuntimeCompensationStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            store.Compensation.Status = compensationExecution.Status;
            store.Compensation.StartedOnUtc = compensationExecution.StartedOnUtc;
            store.Compensation.CompletedOnUtc = compensationExecution.CompletedOnUtc;
            store.Compensation.FailedOnUtc = compensationExecution.FailedOnUtc;
            store.Compensation.RequestPayload = compensationExecution.RequestPayload;
            store.Compensation.ResponsePayload = compensationExecution.ResponsePayload;
            store.Compensation.ErrorMessage = compensationExecution.ErrorMessage;
            store.Compensation.Metadata = compensationExecution.Metadata;
            return Task.CompletedTask;
        }

        public Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Compensation.Id == compensationExecutionId ? store.Compensation : null!);

        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>([store.Compensation]);

        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(
                store.Compensation.Status is "Pending" or "Started" ? [store.Compensation] : []);
    }

    private sealed class InstanceRepositoryStub(RuntimeCompensationStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instance.Status = instance.Status;
            store.Instance.CompensatedOnUtc = instance.CompensatedOnUtc;
            store.Instance.FailedOnUtc = instance.FailedOnUtc;
            store.Instance.FinalOutcome = instance.FinalOutcome;
            store.Instance.ErrorSummary = instance.ErrorSummary;
            store.Instance.LastUpdatedOnUtc = instance.LastUpdatedOnUtc;
            return Task.CompletedTask;
        }

        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Instance);

        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TaskRepositoryStub(RuntimeCompensationStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.SourceTask);
        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>([store.SourceTask]);
        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TransitionRepositoryStub(RuntimeCompensationStore store) : IExecutionTransitionRepository
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
}
