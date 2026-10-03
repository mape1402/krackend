namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using NSubstitute;

public sealed class CompensateInstanceDecisionHandlerTests
{
    [Fact]
    public async Task HandleAsyncFailsCompensationWhenAdapterIsMissing()
    {
        var fixture = new CompensationFixture();
        var instance = CreateInstance();
        var stage = StageExecution(instance.Id);
        var completedTask = CompletedTask(instance.Id, stage.Id, "task-one");
        var compensation = Compensation("commands.undo");
        fixture.Arrange(instance, [completedTask], [stage], CreateResolvedArtifact(
            instance.RuntimeOrchestrationArtifactId,
            Stage("stage-one", MessagingTask("task-one") with { Compensation = compensation })));

        await fixture.Handler.HandleAsync(new CompensateInstanceDecision(
            instance.Id,
            instance.RuntimeOrchestrationArtifactId.ToString(),
            """{"value":"fallback"}"""));

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.True(instance.Metadata["Compensation.TerminalFailure"]!.GetValue<bool>());
        var compensationExecution = Assert.Single(fixture.CreatedCompensations);
        Assert.Equal("Messaging", compensationExecution.CompensationTaskKey);
        Assert.Equal("Failed", compensationExecution.Status);
        Assert.Equal(
            "CompensationTaskRuntimeAdapterNotConfigured",
            compensationExecution.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        Assert.Single(fixture.Transitions, transition => transition.TransitionType == "CompensationFailed");
        Assert.Empty(fixture.RemoteCommands);
    }

    [Fact]
    public async Task HandleAsyncMarksTransformationFailureAsTerminal()
    {
        var fixture = new CompensationFixture();
        var adapter = new RecordingTaskRuntimeAdapter();
        fixture.AdapterRegistry.Adapter = adapter;
        fixture.TransformationExecutor
            .TransformAsync(Arg.Any<OrchestrationTransformationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrchestrationTransformationResult.Failure(
                string.Empty,
                string.Empty,
                new Dictionary<string, JsonNode>
                {
                    ["path"] = JsonValue.Create("$.customer")
                })));
        var instance = CreateInstance();
        var stage = StageExecution(instance.Id);
        var completedTask = CompletedTask(instance.Id, stage.Id, "task-one");
        var compensation = Compensation(
            "commands.undo",
            new TransformationArtifact(EngineType.DSL, null!) { IsEnabled = true });
        fixture.Arrange(instance, [completedTask], [stage], CreateResolvedArtifact(
            instance.RuntimeOrchestrationArtifactId,
            Stage("stage-one", MessagingTask("task-one") with { Compensation = compensation })));

        await fixture.Handler.HandleAsync(new CompensateInstanceDecision(
            instance.Id,
            instance.RuntimeOrchestrationArtifactId.ToString(),
            """{"value":"fallback"}"""));

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal("Compensation transformation failed.", instance.ErrorSummary);
        var compensationExecution = Assert.Single(fixture.CreatedCompensations);
        Assert.Equal("Failed", compensationExecution.Status);
        Assert.Equal(
            "CompensationTransformationFailed",
            compensationExecution.Metadata["TransformationErrorCode"]!.GetValue<string>());
        Assert.Equal(
            "Compensation transformation failed.",
            compensationExecution.Metadata["TransformationErrorMessage"]!.GetValue<string>());
        Assert.Equal("$.customer", compensationExecution.Metadata["Transformation.path"]!.GetValue<string>());
        Assert.Single(fixture.Transitions, transition => transition.TransitionType == "CompensationTransformationFailed");
        Assert.Empty(fixture.RemoteCommands);
    }

    [Fact]
    public async Task HandleAsyncFallsBackToKindWhenAdapterCannotResolveCompensationDestination()
    {
        var fixture = new CompensationFixture();
        fixture.AdapterRegistry.Adapter = new RecordingTaskRuntimeAdapter { ThrowOnDestination = true };
        fixture.ConditionEvaluator
            .EvaluateAsync(Arg.Any<OrchestrationConditionEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OrchestrationConditionEvaluationResult.Success(false)));
        var instance = CreateInstance();
        var stage = StageExecution(instance.Id);
        var completedTask = CompletedTask(instance.Id, stage.Id, "task-one");
        fixture.Arrange(instance, [completedTask], [stage], CreateResolvedArtifact(
            instance.RuntimeOrchestrationArtifactId,
            Stage("stage-one", MessagingTask("task-one") with { Compensation = Compensation("commands.undo") })));

        await fixture.Handler.HandleAsync(new CompensateInstanceDecision(
            instance.Id,
            instance.RuntimeOrchestrationArtifactId.ToString(),
            """{"value":"fallback"}"""));

        var compensationExecution = Assert.Single(fixture.CreatedCompensations);
        Assert.Equal("Messaging", compensationExecution.CompensationTaskKey);
        Assert.Equal("Skipped", compensationExecution.Status);
        Assert.False(compensationExecution.Metadata["ConditionResult"]!.GetValue<bool>());
        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Contains(fixture.Transitions, transition => transition.TransitionType == "CompensationSkipped");
        Assert.Contains(fixture.Transitions, transition => transition.TransitionType == "InstanceCompensated");
    }

    [Fact]
    public async Task HandleAsyncCompletesWhenCompletedTasksHaveNoRunnableCompensation()
    {
        var fixture = new CompensationFixture();
        var instance = CreateInstance();
        var stage = StageExecution(instance.Id);
        var missingArtifactTask = CompletedTask(instance.Id, stage.Id, "missing-task");
        var taskWithoutCompensation = CompletedTask(instance.Id, stage.Id, "task-without-compensation");
        fixture.Arrange(instance, [missingArtifactTask, taskWithoutCompensation], [stage], CreateResolvedArtifact(
            instance.RuntimeOrchestrationArtifactId,
            Stage("stage-one", MessagingTask("task-without-compensation"))));

        await fixture.Handler.HandleAsync(new CompensateInstanceDecision(
            instance.Id,
            instance.RuntimeOrchestrationArtifactId.ToString(),
            """{"value":"fallback"}"""));

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Empty(fixture.CreatedCompensations);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceCompensated", transition.TransitionType);
    }

    [Fact]
    public void PrivateCompensationHelpersHandleFallbacksAndNullDiagnostics()
    {
        var instance = CreateInstance();
        instance.SagaId = " ";
        var trigger = new TriggerBindingArtifact(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(null!, "orders.created", new SemanticVersion(1, 0, 0)),
            true,
            null!,
            Compensation("commands.undo"));
        var metadata = new Dictionary<string, JsonNode>();

        var sagaId = InvokePrivateStatic<string>("GetSagaId", instance);
        var task = InvokePrivateStatic<TaskArtifact>("CreateTriggerCompensationTask", trigger, "trigger.orders.created");
        InvokePrivateStatic<object?>("CopyDiagnostics", metadata, null!, "Condition");
        InvokePrivateStatic<object?>(
            "CopyDiagnostics",
            metadata,
            new Dictionary<string, JsonNode>
            {
                ["nullable"] = null!,
                ["path"] = JsonValue.Create("$.customer")!
            },
            "Transformation");

        Assert.Equal(instance.Id.ToString(), sagaId);
        Assert.NotNull(task);
        Assert.Equal("trigger.orders.created", task!.Name);
        Assert.True(metadata.ContainsKey("Transformation.nullable"));
        Assert.Null(metadata["Transformation.nullable"]);
        Assert.Equal("$.customer", metadata["Transformation.path"]!.GetValue<string>());
    }

    private static OrchestrationInstance CreateInstance()
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "order.flow",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = OrchestrationInstanceStatus.Failed,
            CurrentStageKey = "stage-one",
            CurrentTaskKey = "task-one",
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-10),
            LastUpdatedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            ErrorSummary = "task failed",
            SnapshotPayload = JsonNode.Parse("""{"value":"snapshot"}""")!
        };

    private static StageExecution StageExecution(Id instanceId)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageKey = "stage-one",
            Order = 1,
            Status = StageExecutionStatus.Completed,
            CompletedOnUtc = DateTime.UtcNow.AddMinutes(-2)
        };

    private static TaskExecution CompletedTask(Id instanceId, Id stageExecutionId, string taskKey)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageExecutionId,
            TaskKey = taskKey,
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Completed,
            CompletedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastAttemptNumber = 1,
            CorrelationId = "task-correlation"
        };

    private static ResolvedOrchestrationArtifact CreateResolvedArtifact(Id artifactId, params StageArtifact[] stages)
    {
        var artifact = new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            "order.flow",
            "Order Flow",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("checksum"),
            Array.Empty<TriggerBindingArtifact>(),
            Array.Empty<VariableDefinitionArtifact>(),
            stages);

        return new ResolvedOrchestrationArtifact
        {
            RuntimeArtifact = new RuntimeOrchestrationArtifact
            {
                Id = artifactId,
                OrchestrationDefinitionKey = artifact.Key,
                ArtifactType = "orchestration.deploy",
                Version = artifact.Version,
                ArtifactChecksum = artifact.Checksum,
                ArtifactPayload = JsonNode.Parse("""{}""")!
            },
            Artifact = artifact
        };
    }

    private static StageArtifact Stage(string key, params TaskArtifact[] tasks)
        => new(
            Id.New(),
            key,
            key,
            1,
            true,
            null!,
            tasks,
            Array.Empty<ParallelGroupArtifact>(),
            Array.Empty<BranchRuleArtifact>());

    private static TaskArtifact MessagingTask(string key)
        => new(
            Id.New(),
            key,
            key,
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null!,
            null!,
            MessagingConfiguration("orders.forward"),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static CompensationArtifact Compensation(
        string topic,
        TransformationArtifact? transformation = null)
        => new(
            TaskKind.Messaging,
            transformation,
            null!,
            MessagingConfiguration(topic),
            null!,
            null!,
            TaskDispatchType.FireAndForget);

    private static MessagingTaskConfigurationArtifact MessagingConfiguration(string topic)
        => new(topic, new SemanticVersion(1, 0, 0), null!);

    private static T? InvokePrivateStatic<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(CompensateInstanceDecisionHandler).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (T?)method.Invoke(null, arguments);
    }

    private sealed class CompensationFixture
    {
        public CompensationFixture()
        {
            InstanceRepository.Update(Arg.Any<OrchestrationInstance>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            CompensationRepository
                .Create(Arg.Do<CompensationExecution>(execution => CreatedCompensations.Add(execution)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            CompensationRepository
                .Update(Arg.Do<CompensationExecution>(execution => UpdatedCompensations.Add(execution)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Dispatcher
                .DispatchAsync(Arg.Do<RemoteCommand>(command => RemoteCommands.Add(command)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            ConditionEvaluator
                .EvaluateAsync(Arg.Any<OrchestrationConditionEvaluationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(OrchestrationConditionEvaluationResult.Success(true)));
            PayloadContextFactory
                .Create(
                    Arg.Any<OrchestrationInstance>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<IReadOnlyCollection<MetadataDescriptorArtifact>>())
                .Returns(call => new OrchestrationPayloadContext
                {
                    StageKey = (string)call[1],
                    TaskKey = (string)call[2],
                    ContextPayload = JsonNode.Parse("""{}""")!,
                    TriggerPayload = JsonNode.Parse("""{}""")!
                });
            TransformationExecutor
                .TransformAsync(Arg.Any<OrchestrationTransformationRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(OrchestrationTransformationResult.Success(JsonNode.Parse("""{"value":"transformed"}""")!)));

            Handler = new CompensateInstanceDecisionHandler(
                ArtifactResolver,
                InstanceRepository,
                StageRepository,
                TaskRepository,
                CompensationRepository,
                TransitionRepository,
                Dispatcher,
                AdapterRegistry,
                ConditionEvaluator,
                PayloadContextFactory,
                TransformationExecutor);
        }

        public IRuntimeArtifactResolver ArtifactResolver { get; } =
            Substitute.For<IRuntimeArtifactResolver>();

        public IOrchestrationInstanceRepository InstanceRepository { get; } =
            Substitute.For<IOrchestrationInstanceRepository>();

        public IStageExecutionRepository StageRepository { get; } =
            Substitute.For<IStageExecutionRepository>();

        public ITaskExecutionRepository TaskRepository { get; } =
            Substitute.For<ITaskExecutionRepository>();

        public ICompensationExecutionRepository CompensationRepository { get; } =
            Substitute.For<ICompensationExecutionRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } =
            Substitute.For<IExecutionTransitionRepository>();

        public IRemoteCommandDispatcher Dispatcher { get; } =
            Substitute.For<IRemoteCommandDispatcher>();

        public TestTaskRuntimeAdapterRegistry AdapterRegistry { get; } = new();

        public IOrchestrationConditionEvaluator ConditionEvaluator { get; } =
            Substitute.For<IOrchestrationConditionEvaluator>();

        public IOrchestrationPayloadContextFactory PayloadContextFactory { get; } =
            Substitute.For<IOrchestrationPayloadContextFactory>();

        public IOrchestrationTransformationExecutor TransformationExecutor { get; } =
            Substitute.For<IOrchestrationTransformationExecutor>();

        public CompensateInstanceDecisionHandler Handler { get; }

        public List<CompensationExecution> CreatedCompensations { get; } = [];

        public List<CompensationExecution> UpdatedCompensations { get; } = [];

        public List<ExecutionTransition> Transitions { get; } = [];

        public List<RemoteCommand> RemoteCommands { get; } = [];

        public void Arrange(
            OrchestrationInstance instance,
            IReadOnlyCollection<TaskExecution> completedTasks,
            IReadOnlyCollection<StageExecution> stages,
            ResolvedOrchestrationArtifact resolvedArtifact)
        {
            InstanceRepository
                .GetById(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(instance));
            TaskRepository
                .GetByInstanceId(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(completedTasks));
            ArtifactResolver
                .ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(resolvedArtifact));

            foreach (var stage in stages)
            {
                StageRepository
                    .GetById(stage.Id, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(stage));
            }
        }
    }

    private sealed class TestTaskRuntimeAdapterRegistry : ITaskRuntimeAdapterRegistry
    {
        public ITaskRuntimeAdapter? Adapter { get; set; }

        public bool TryGet(TaskKind taskKind, out ITaskRuntimeAdapter adapter)
        {
            adapter = Adapter!;
            return adapter is not null && taskKind == adapter.TaskKind;
        }
    }

    private sealed class RecordingTaskRuntimeAdapter : ITaskRuntimeAdapter
    {
        public TaskKind TaskKind => TaskKind.Messaging;

        public bool ThrowOnDestination { get; init; }

        public RuntimeArtifactCompatibilityValidationResult ValidateTask(TaskArtifact task, string stageKey)
            => throw new NotSupportedException();

        public RuntimeArtifactCompatibilityValidationResult ValidateCompensation(TaskArtifact task, string stageKey)
            => throw new NotSupportedException();

        public string GetDestination(TaskArtifact task)
            => throw new NotSupportedException();

        public string GetCompensationDestination(CompensationArtifact compensation)
            => ThrowOnDestination ? throw new InvalidOperationException("Destination unavailable.") : "commands.undo";

        public Task<TaskRuntimeCommandDescriptor> BuildCommandAsync(
            TaskRuntimeCommandRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TaskRuntimeCommandDescriptor> BuildCompensationCommandAsync(
            TaskRuntimeCompensationCommandRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TaskRuntimeCommandDescriptor
            {
                Transport = RemoteCommandTransport.Messaging,
                SettingsPayload = """{"topic":"commands.undo"}"""
            });
    }
}
