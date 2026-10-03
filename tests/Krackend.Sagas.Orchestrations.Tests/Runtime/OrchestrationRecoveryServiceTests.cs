namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using NSubstitute;

public sealed class OrchestrationRecoveryServiceTests
{
    [Fact]
    public async Task ReplayRejectsInvalidInstanceId()
    {
        var fixture = new RecoveryFixture();

        var result = await fixture.Service.ReplayAsync("not-a-valid-id");

        Assert.False(result.Succeeded);
        Assert.Equal(string.Empty, result.Status);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.InstanceRepository.DidNotReceiveWithAnyArgs().GetById(default, default);
    }

    [Fact]
    public async Task AbortRejectsFinalInstance()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Completed);
        fixture.InstanceRepository
            .GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));

        var result = await fixture.Service.AbortAsync(instance.Id.ToString(), "operator stop");

        Assert.False(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Completed.ToString(), result.Status);
        await fixture.InstanceRepository.DidNotReceiveWithAnyArgs().Update(default!, default);
        await fixture.TransitionRepository.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task AbortMarksRecoverableInstanceAsAborted()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            failedOnUtc: null,
            stoppedOnUtc: null,
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-5),
            errorSummary: "timeout");
        fixture.InstanceRepository
            .GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));

        var result = await fixture.Service.AbortAsync(instance.Id.ToString(), "  operator fixed externally  ");

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Aborted.ToString(), result.Status);
        Assert.Equal(OrchestrationInstanceStatus.Aborted, instance.Status);
        Assert.NotNull(instance.FailedOnUtc);
        Assert.NotNull(instance.StoppedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        Assert.Equal("operator fixed externally", instance.ErrorSummary);
        Assert.Equal("operator fixed externally", instance.Metadata["Recovery.AbortReason"]!.GetValue<string>());
        Assert.NotNull(instance.Metadata["Recovery.AbortedOnUtc"]);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceAborted", transition.TransitionType);
        Assert.Equal(OrchestrationInstanceStatus.DeadLettered.ToString(), transition.FromStatus);
        Assert.Equal(OrchestrationInstanceStatus.Aborted.ToString(), transition.ToStatus);
    }

    [Fact]
    public async Task ReplayFailedTaskDispatchesRetryFromArtifactDefinition()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            currentStageKey: "stage-one",
            currentTaskKey: "reserve-stock",
            errorSummary: "task failed");
        var stageExecution = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "stage-one",
            Order = 1,
            Status = StageExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-2),
            ErrorSummary = "stage failed"
        };
        var taskExecution = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stageExecution.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastAttemptNumber = 2,
            CorrelationId = "task-correlation",
            Metadata =
            {
                ["RetrySuppressed"] = JsonValue.Create(true)
            }
        };
        var taskArtifact = MessagingTask("reserve-stock");
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", taskArtifact)),
            [stageExecution],
            [taskExecution]);

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running.ToString(), result.Status);
        Assert.False(taskExecution.Metadata.ContainsKey("RetrySuppressed"));
        Assert.True(taskExecution.Metadata["ManualReplay"]!.GetValue<bool>());
        Assert.Equal("operator", instance.Metadata["Recovery.ReplayRequestedBy"]!.GetValue<string>());
        var dispatch = Assert.Single(fixture.Dispatches);
        Assert.Equal(TaskAttemptDispatchKind.Retry, dispatch.Kind);
        Assert.Same(instance, dispatch.Instance);
        Assert.Equal(stageExecution.Id, dispatch.StageExecutionId);
        Assert.Equal("stage-one", dispatch.StageKey);
        Assert.Same(taskArtifact, dispatch.Task);
        Assert.Same(taskExecution, dispatch.TaskExecution);
        Assert.Contains("snapshot", dispatch.Payload, StringComparison.Ordinal);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceReplayRequested", transition.TransitionType);
        Assert.Equal(stageExecution.Id, transition.StageExecutionId);
        Assert.Equal(taskExecution.Id, transition.TaskExecutionId);
    }

    [Fact]
    public async Task ReplayFailedTaskRejectsWhenArtifactDefinitionIsUnavailable()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var failedTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "missing-task",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("other-stage", MessagingTask("other-task"))),
            Array.Empty<StageExecution>(),
            [failedTask]);

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Dispatches);
        Assert.Empty(fixture.Transitions);
    }

    [Fact]
    public async Task ReplayFailedStageResetsStageAndContinuesEngine()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            currentStageKey: "stage-two",
            currentTaskKey: "charge-card",
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-10),
            errorSummary: "stage failed");
        var failedStage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "stage-two",
            Order = 2,
            Status = StageExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-2),
            ErrorSummary = "stage failed"
        };
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-two", MessagingTask("charge-card"))),
            [failedStage],
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString(), """{"value":"operator-payload"}""");

        Assert.True(result.Succeeded);
        Assert.Equal(StageExecutionStatus.Running, failedStage.Status);
        Assert.Null(failedStage.FailedOnUtc);
        Assert.Null(failedStage.ErrorSummary);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Equal("stage-two", instance.CurrentStageKey);
        Assert.Equal(string.Empty, instance.CurrentTaskKey);
        Assert.Null(instance.FailedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        var forward = Assert.Single(fixture.Forwards);
        Assert.Equal(instance.RuntimeOrchestrationArtifactId.ToString(), forward.ArtifactId);
        Assert.Equal("operator-payload", forward.Payload!["value"]!.GetValue<string>());
        Assert.Equal("stage-two", forward.MessageMetadata.CurrentStage);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("StageReplayRequested", transition.TransitionType);
        Assert.Equal(failedStage.Id, transition.StageExecutionId);
    }

    [Fact]
    public async Task ReplayRecoverableInstanceWithoutFailedWorkContinuesEngine()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.Waiting,
            currentStageKey: "stage-one",
            currentTaskKey: string.Empty,
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-3),
            errorSummary: "waiting");
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Null(instance.FailedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        Assert.Null(instance.ErrorSummary);
        Assert.Equal("operator", instance.Metadata["Recovery.ReplayRequestedBy"]!.GetValue<string>());
        var forward = Assert.Single(fixture.Forwards);
        Assert.Equal(instance.SagaId, forward.MessageMetadata.SagaId);
        Assert.Equal(instance.Id.ToString(), forward.MessageMetadata.OrchestrationInstanceId);
        Assert.Equal(instance.CorrelationId, forward.MessageMetadata.CorrelationId);
        Assert.Equal("snapshot", forward.Payload!["value"]!.GetValue<string>());
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceReplayRequested", transition.TransitionType);
        Assert.Null(transition.StageExecutionId);
        Assert.Null(transition.TaskExecutionId);
    }

    private static OrchestrationInstance CreateInstance(
        OrchestrationInstanceStatus status,
        string currentStageKey = "stage-one",
        string currentTaskKey = "reserve-stock",
        DateTime? failedOnUtc = null,
        DateTime? stoppedOnUtc = null,
        DateTime? waitingSinceUtc = null,
        string errorSummary = "")
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "order.flow",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = status,
            CurrentStageKey = currentStageKey,
            CurrentTaskKey = currentTaskKey,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-20),
            LastUpdatedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            FailedOnUtc = failedOnUtc ?? (status is OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.DeadLettered ? DateTime.UtcNow.AddMinutes(-1) : null),
            StoppedOnUtc = stoppedOnUtc,
            WaitingSinceUtc = waitingSinceUtc,
            ErrorSummary = errorSummary,
            SnapshotPayload = JsonNode.Parse("""{"value":"snapshot"}""")!
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
            new MessagingTaskConfigurationArtifact(
                "orders.reserve",
                new SemanticVersion(1, 0, 0),
                null!),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private sealed class RecoveryFixture
    {
        public RecoveryFixture()
        {
            InstanceRepository.Update(Arg.Any<OrchestrationInstance>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            StageRepository.Update(Arg.Any<StageExecution>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TaskAttemptDispatcher
                .DispatchAsync(Arg.Do<TaskAttemptDispatchRequest>(request => Dispatches.Add(request)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            SagaEngine
                .OrchestrateAsync(Arg.Do<ForwardIntent>(intent => Forwards.Add(intent)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Service = new DefaultOrchestrationRecoveryService(
                InstanceRepository,
                StageRepository,
                TaskRepository,
                TransitionRepository,
                ArtifactResolver,
                TaskAttemptDispatcher,
                SagaEngine);
        }

        public IOrchestrationInstanceRepository InstanceRepository { get; } =
            Substitute.For<IOrchestrationInstanceRepository>();

        public IStageExecutionRepository StageRepository { get; } =
            Substitute.For<IStageExecutionRepository>();

        public ITaskExecutionRepository TaskRepository { get; } =
            Substitute.For<ITaskExecutionRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } =
            Substitute.For<IExecutionTransitionRepository>();

        public IRuntimeArtifactResolver ArtifactResolver { get; } =
            Substitute.For<IRuntimeArtifactResolver>();

        public ITaskAttemptDispatcher TaskAttemptDispatcher { get; } =
            Substitute.For<ITaskAttemptDispatcher>();

        public ISagaEngine SagaEngine { get; } =
            Substitute.For<ISagaEngine>();

        public DefaultOrchestrationRecoveryService Service { get; }

        public List<ExecutionTransition> Transitions { get; } = [];

        public List<TaskAttemptDispatchRequest> Dispatches { get; } = [];

        public List<ForwardIntent> Forwards { get; } = [];

        public void ArrangeReplay(
            OrchestrationInstance instance,
            ResolvedOrchestrationArtifact resolvedArtifact,
            IReadOnlyCollection<StageExecution> stages,
            IReadOnlyCollection<TaskExecution> tasks)
        {
            InstanceRepository
                .GetById(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(instance));
            ArtifactResolver
                .ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(resolvedArtifact));
            StageRepository
                .GetByInstanceId(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(stages));
            TaskRepository
                .GetByInstanceId(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(tasks));
        }
    }
}
