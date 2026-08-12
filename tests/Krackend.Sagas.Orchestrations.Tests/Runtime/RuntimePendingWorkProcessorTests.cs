using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
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
        var store = RuntimePendingWorkStore.Create(now, withTimeoutPolicy: false);
        var compensation = PendingCompensation(store.Instance.Id, store.Task.Id);
        store.Compensations.Add(compensation);

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.WaitingTaskCount);
        Assert.Equal(1, result.WaitingAttemptCount);
        Assert.Equal(1, result.PendingCompensationCount);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingTaskTimeout && x.Id == store.Task.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingAttemptTimeout && x.Id == store.Attempt.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.PendingCompensation && x.Id == compensation.Id);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnWaitingItemsNewerThanScanInstant()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(1), withTimeoutPolicy: false);

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnCompletedResponses()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), withTimeoutPolicy: true);
        store.Task.Status = TaskExecutionStatus.Completed;
        store.Attempt.Status = TaskExecutionStatus.Completed;

        var result = await CreateProcessor(store).ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ProcessDueWork_AppliesFailTimeoutPolicyAndStopsInstance()
    {
        var now = DateTime.UtcNow;
        var store = RuntimePendingWorkStore.Create(now.AddMinutes(-5), withTimeoutPolicy: true);

        await CreateProcessor(store).ProcessDueWork(now);

        Assert.Equal(TaskExecutionStatus.TimedOut, store.Attempt.Status);
        Assert.Equal(TaskExecutionStatus.Failed, store.Task.Status);
        Assert.Equal(StageExecutionStatus.Failed, store.Stage.Status);
        Assert.Equal(OrchestrationInstanceStatus.Failed, store.Instance.Status);
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimedOut");
        Assert.Contains(store.Transitions, x => x.TransitionType == "TaskTimeoutPolicyApplied");
        Assert.Contains(store.Transitions, x => x.TransitionType == "InstanceFailed");
    }

    private static RuntimePendingWorkProcessor CreateProcessor(RuntimePendingWorkStore store)
        => new(
            new TaskRepositoryStub(store),
            new AttemptRepositoryStub(store),
            new CompensationRepositoryStub(store),
            new InstanceRepositoryStub(store),
            new StageRepositoryStub(store),
            new ArtifactRepositoryStub(store),
            new TransitionRepositoryStub(store),
            new RuntimeTimeoutPolicyEvaluator(),
            new RuntimeErrorPolicyResolver(),
            new NoopRuntimeReactiveEventPublisher());

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
        public required RuntimeOrchestrationArtifact Artifact { get; set; }
        public List<CompensationExecution> Compensations { get; } = new();
        public List<ExecutionTransition> Transitions { get; } = new();

        public static RuntimePendingWorkStore Create(DateTime waitingSinceUtc, bool withTimeoutPolicy)
        {
            var artifactId = Id.New();
            var instanceId = Id.New();
            var stageId = Id.New();
            var taskId = Id.New();
            return new RuntimePendingWorkStore
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
                Attempt = new TaskExecutionAttempt
                {
                    Id = Id.New(),
                    TaskExecutionId = taskId,
                    AttemptNumber = 1,
                    Status = TaskExecutionStatus.WaitingResponse,
                    WaitingSinceUtc = waitingSinceUtc
                },
                Artifact = new RuntimeOrchestrationArtifact
                {
                    Id = artifactId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = "order.fulfillment",
                    ArtifactType = "orchestration",
                    Version = new SemanticVersion(1, 0, 0),
                    ArtifactChecksum = new Checksum("checksum"),
                    ArtifactPayload = BuildArtifactPayload(withTimeoutPolicy),
                    IsActive = true,
                    DeployedOnUtc = waitingSinceUtc,
                    ActivatedOnUtc = waitingSinceUtc
                }
            };
        }

        private static JsonNode BuildArtifactPayload(bool withTimeoutPolicy)
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
                      "TimeoutPolicy": {{(withTimeoutPolicy ? """{ "Timeout": { "Value": "00:00:01" }, "TimeoutBehavior": 0, "TimeoutBehaviorPolicy": { "ErrorCode": "ReserveTimeout" } }""" : "{}")}}
                    }
                  ]
                }
              ]
            }
            """)!;
    }

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
        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>([store.Task]);
        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(store.Task.Status == TaskExecutionStatus.WaitingResponse &&
                                                                   store.Task.WaitingSinceUtc <= dueBeforeUtc
                ? [store.Task]
                : []);
    }

    private sealed class AttemptRepositoryStub(RuntimePendingWorkStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            store.Attempt = attempt;
            return Task.CompletedTask;
        }
        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([store.Attempt]);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(store.Attempt.Status == TaskExecutionStatus.WaitingResponse &&
                                                                          store.Attempt.WaitingSinceUtc <= dueBeforeUtc
                ? [store.Attempt]
                : []);
    }

    private sealed class CompensationRepositoryStub(RuntimePendingWorkStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            store.Compensations.Add(compensationExecution);
            return Task.CompletedTask;
        }
        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<StageExecution>>([store.Stage]);
    }

    private sealed class ArtifactRepositoryStub(RuntimePendingWorkStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
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
}
