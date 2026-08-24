using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeDiagnosticsReaderTests
{
    [Fact]
    public async Task GetDetail_ReturnsCompleteTraceWithFunctionalAndTechnicalTimelines()
    {
        var store = RuntimeDiagnosticsStore.Create();
        var reader = CreateReader(store);

        var detail = await reader.GetDetail(store.Instance.Id.ToString());

        Assert.Equal(store.Instance.Id.ToString(), detail.Instance.Id);
        Assert.Equal("corr-123", detail.Instance.CorrelationId);
        Assert.Equal("orders", detail.Instance.OrchestrationDefinitionKey);
        Assert.Equal("reserve-stock", Assert.Single(detail.Stages).StageKey);
        Assert.Equal("reserve", Assert.Single(detail.Tasks).TaskKey);
        Assert.Equal(store.Task.Id.ToString(), Assert.Single(detail.Compensations).SourceTaskExecutionId);
        Assert.Equal("reserve", Assert.Single(detail.Compensations).SourceTaskKey);
        Assert.Equal("release-stock", Assert.Single(detail.Compensations).CompensationTaskKey);

        var task = Assert.Single(detail.Tasks);
        Assert.Equal(store.Instance.Id.ToString(), task.SagaId);
        Assert.Equal("StopAndCompensate", task.OnErrorPolicy);
        Assert.Equal(store.Dispatch.Id.ToString(), Assert.Single(task.Attempts).DispatchId);
        Assert.Equal("orders.reserve", Assert.Single(task.Attempts).Dispatch.Destination);

        Assert.Collection(
            detail.FunctionalTimeline,
            item =>
            {
                Assert.Equal("TaskCompleted", item.TransitionType);
                Assert.Equal("reserve", item.TaskKey);
                Assert.Equal("reserve-stock", item.StageKey);
            });

        Assert.Collection(
            detail.TechnicalTimeline,
            item =>
            {
                Assert.Equal("TaskDispatchCreated", item.TransitionType);
                Assert.Equal("reserve", item.TaskKey);
            });

        Assert.Collection(
            detail.Transitions,
            first => Assert.Equal(store.OlderTransition.Id.ToString(), first.Id),
            second => Assert.Equal(store.NewerTransition.Id.ToString(), second.Id));
    }

    private static RuntimeDiagnosticsReader CreateReader(RuntimeDiagnosticsStore store)
    {
        return new RuntimeDiagnosticsReader(
            new ArtifactRepositoryStub(store),
            new InstanceRepositoryStub(store),
            new StageRepositoryStub(store),
            new TaskRepositoryStub(store),
            new AttemptRepositoryStub(store),
            new DispatchRepositoryStub(store),
            new CompensationRepositoryStub(store),
            new TransitionRepositoryStub(store),
            Options.Create(new OrchestratorRuntimeWebUIOptions { EnvironmentKey = "local" }));
    }

    private sealed class RuntimeDiagnosticsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public required RuntimeOrchestrationArtifact Artifact { get; init; }
        public required OrchestrationInstance Instance { get; init; }
        public required StageExecution Stage { get; init; }
        public required TaskExecution Task { get; init; }
        public required TaskExecutionAttempt Attempt { get; init; }
        public required TaskDispatch Dispatch { get; init; }
        public required CompensationExecution Compensation { get; init; }
        public required ExecutionTransition OlderTransition { get; init; }
        public required ExecutionTransition NewerTransition { get; init; }

        public static RuntimeDiagnosticsStore Create()
        {
            var instanceId = Id.New();
            var artifactId = Id.New();
            var stageId = Id.New();
            var taskId = Id.New();
            var attemptId = Id.New();
            var dispatchId = Id.New();
            var started = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
            var artifact = BuildArtifact(stageId, taskId);

            return new RuntimeDiagnosticsStore
            {
                Artifact = new RuntimeOrchestrationArtifact
                {
                    Id = artifactId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = artifact.Key,
                    ArtifactType = "orchestration-version-snapshot",
                    SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
                    Version = artifact.Version,
                    ArtifactChecksum = artifact.Checksum,
                    ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions)!,
                    IsActive = true,
                    LoadedToCache = true,
                    DeployedOnUtc = started.AddMinutes(-1),
                    ActivatedOnUtc = started.AddMinutes(-1),
                    Notes = "Runtime diagnostics test artifact."
                },
                Instance = new OrchestrationInstance
                {
                    Id = instanceId,
                    EnvironmentKey = "local",
                    OrchestrationDefinitionKey = "orders",
                    RuntimeOrchestrationArtifactId = artifactId,
                    TriggerIntakeId = Id.New(),
                    CorrelationId = "corr-123",
                    ExecutionKey = "orders:corr-123",
                    Status = OrchestrationInstanceStatus.Completed,
                    CurrentStageKey = "reserve-stock",
                    CurrentTaskKey = "reserve",
                    StartedOnUtc = started,
                    LastUpdatedOnUtc = started.AddSeconds(5),
                    CompletedOnUtc = started.AddSeconds(5),
                    SnapshotPayload = JsonNode.Parse("""{"orderId":"A1"}""")
                },
                Stage = new StageExecution
                {
                    Id = stageId,
                    OrchestrationInstanceId = instanceId,
                    StageKey = "reserve-stock",
                    Order = 1,
                    Status = StageExecutionStatus.Completed,
                    StartedOnUtc = started,
                    CompletedOnUtc = started.AddSeconds(4)
                },
                Task = new TaskExecution
                {
                    Id = taskId,
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskKey = "reserve",
                    TaskKind = TaskKind.Messaging,
                    ExecutionMode = TaskExecutionMode.Sequential,
                    Status = TaskExecutionStatus.Completed,
                    OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
                    AwaitResponse = true,
                    StartedOnUtc = started.AddSeconds(1),
                    CompletedOnUtc = started.AddSeconds(3),
                    LastAttemptNumber = 1,
                    CorrelationId = "task-corr",
                    OutputVariablesPayload = JsonNode.Parse("""{"reserved":true}""")
                },
                Attempt = new TaskExecutionAttempt
                {
                    Id = attemptId,
                    TaskExecutionId = taskId,
                    AttemptNumber = 1,
                    Status = TaskExecutionStatus.Completed,
                    StartedOnUtc = started.AddSeconds(1),
                    CompletedOnUtc = started.AddSeconds(3),
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}"""),
                    ResponsePayload = JsonNode.Parse("""{"ok":true}"""),
                    DispatchId = dispatchId
                },
                Dispatch = new TaskDispatch
                {
                    Id = dispatchId,
                    TaskExecutionAttemptId = attemptId,
                    DispatchType = "Messaging",
                    Destination = "orders.reserve",
                    DispatchStatus = "Sent",
                    CommandId = "cmd-1",
                    CorrelationId = "task-corr",
                    SentOnUtc = started.AddSeconds(1),
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}""")
                },
                Compensation = new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    SourceTaskExecutionId = taskId,
                    CompensationTaskKey = "release-stock",
                    Status = "Pending",
                    RequestPayload = JsonNode.Parse("""{"sku":"SKU-1"}""")
                },
                OlderTransition = new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskExecutionId = taskId,
                    TaskExecutionAttemptId = attemptId,
                    TransitionType = "TaskCompleted",
                    FromStatus = "Running",
                    ToStatus = "Completed",
                    OccurredOnUtc = started.AddSeconds(3),
                    Message = "Task completed",
                    ProducedBy = "Runtime"
                },
                NewerTransition = new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instanceId,
                    StageExecutionId = stageId,
                    TaskExecutionId = taskId,
                    TaskExecutionAttemptId = attemptId,
                    TransitionType = "TaskDispatchCreated",
                    FromStatus = "Pending",
                    ToStatus = "Sent",
                    OccurredOnUtc = started.AddSeconds(4),
                    Message = "Dispatch sent",
                    ProducedBy = "Runtime"
                }
            };
        }

        private static OrchestrationArtifact BuildArtifact(Id stageId, Id taskId)
        {
            var version = new SemanticVersion(1, 0, 0);
            var schemaBinding = new SchemaBindingArtifact(
                taskId,
                ElementType.Task,
                stageId,
                taskId,
                "orders.reserve",
                version,
                Id.New(),
                false)
            {
                IsValidationEnabled = false
            };

            var configuration = new MessagingTaskConfigurationArtifact("orders.reserve", version, schemaBinding);

            return new OrchestrationArtifact(
                Id.New(),
                Id.New(),
                "orders",
                "Orders",
                "sales",
                version,
                new Checksum("test"),
                [],
                [],
                [
                    new StageArtifact(
                        stageId,
                        "reserve-stock",
                        "Reserve stock",
                        1,
                        DisabledCondition(),
                        [
                            new TaskArtifact(
                                taskId,
                                "reserve",
                                "Reserve inventory",
                                1,
                                string.Empty,
                                TaskKind.Messaging,
                                TaskExecutionMode.Sequential,
                                null,
                                DisabledCondition(),
                                DisabledTransformation(),
                                configuration,
                                DefaultRetryPolicy(),
                                DefaultTimeoutPolicy(),
                                OnErrorPolicy.StopAndCompensate,
                                DefaultCompensation(configuration),
                                TaskDispatchType.FireAndWaitCallback,
                                true)
                        ],
                        [],
                        [],
                        "Reserve inventory before charging the order.")
                ],
                "Test artifact.");
        }

        private static ExecutionConditionArtifact DisabledCondition()
            => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")))
            {
                IsEnabled = false
            };

        private static TransformationArtifact DisabledTransformation()
            => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
            {
                IsEnabled = false
            };

        private static RetryPolicyArtifact DefaultRetryPolicy()
            => new(
                0,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                [],
                true);

        private static TimeoutPolicyArtifact DefaultTimeoutPolicy()
            => new(
                Duration.FromMinutes(5),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("TASK_TIMEOUT"));

        private static CompensationArtifact DefaultCompensation(ITaskConfigurationArtifact configuration)
            => new(
                TaskKind.Messaging,
                DisabledTransformation(),
                DisabledCondition(),
                configuration,
                DefaultRetryPolicy(),
                DefaultTimeoutPolicy(),
                TaskDispatchType.FireAndForget);
    }

    private sealed class ArtifactRepositoryStub(RuntimeDiagnosticsStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>([store.Artifact]);
        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifact);
    }

    private sealed class InstanceRepositoryStub(RuntimeDiagnosticsStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult(store.Instance);
        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([store.Instance]);
        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default) => Task.FromResult(new RuntimeInstanceSummary(0, 0, 1, 0, recentSinceUtc));
    }

    private sealed class StageRepositoryStub(RuntimeDiagnosticsStore store) : IStageExecutionRepository
    {
        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Stage);
        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<StageExecution>>([store.Stage]);
    }

    private sealed class TaskRepositoryStub(RuntimeDiagnosticsStore store) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default) => Task.FromResult(store.Task);
        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>([store.Task]);
        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecution>>(Array.Empty<TaskExecution>());
    }

    private sealed class AttemptRepositoryStub(RuntimeDiagnosticsStore store) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Attempt);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>([store.Attempt]);
        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(Array.Empty<TaskExecutionAttempt>());
    }

    private sealed class DispatchRepositoryStub(RuntimeDiagnosticsStore store) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string? externalReference = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailed(Id dispatchId, string failureReason, string? externalReference = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default) => Task.FromResult(store.Dispatch);
        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(Array.Empty<TaskDispatch>());
    }

    private sealed class CompensationRepositoryStub(RuntimeDiagnosticsStore store) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default) => Task.FromResult(store.Compensation);
        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CompensationExecution>>([store.Compensation]);
        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(Array.Empty<CompensationExecution>());
    }

    private sealed class TransitionRepositoryStub(RuntimeDiagnosticsStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>([store.NewerTransition, store.OlderTransition]);
        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>([store.NewerTransition, store.OlderTransition]);
        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>(Array.Empty<RuntimeTrafficPoint>());
    }
}
