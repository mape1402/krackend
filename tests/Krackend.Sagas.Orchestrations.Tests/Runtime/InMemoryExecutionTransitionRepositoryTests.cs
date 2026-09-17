namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class InMemoryExecutionTransitionRepositoryTests
{
    [Fact]
    public async Task CreatePublishesReactiveEventWithInstanceStageTaskAndArtifactMetadata()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var seeded = await SeedRuntimeExecution(services);
        var repository = services.GetRequiredService<IExecutionTransitionRepository>();
        var transition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = seeded.InstanceId,
            StageExecutionId = seeded.StageId,
            TaskExecutionId = seeded.TaskId,
            TaskExecutionAttemptId = seeded.AttemptId,
            TransitionType = "TaskCompleted",
            FromStatus = "Running",
            ToStatus = "Completed",
            OccurredOnUtc = DateTime.UtcNow,
            Message = "Task completed.",
            Payload = JsonNode.Parse("""{"source":"test"}"""),
            ProducedBy = "tests"
        };

        await repository.Create(transition);

        var published = Assert.Single(publisher.Events);
        Assert.Equal(RuntimeReactiveEventNames.TaskCompleted, published.EventName);
        Assert.Equal("sales.sale.created", published.OrchestrationDefinitionKey);
        Assert.Equal("1.2.3", published.OrchestrationVersion);
        Assert.Equal("correlation-1", published.CorrelationId);
        Assert.Equal("saga-1", published.SagaId);
        Assert.Equal("inventory-reservation", published.StageKey);
        Assert.Equal("inventories.reserve", published.TaskKey);
        Assert.Equal("Completed", published.ToStatus);
        Assert.Equal("tests", published.ProducedBy);
    }

    [Fact]
    public async Task GetByInstanceIdOrdersTransitionsAscendingAndGetRecentOrdersDescending()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var seeded = await SeedRuntimeExecution(services);
        var repository = services.GetRequiredService<IExecutionTransitionRepository>();
        var first = Transition(seeded.InstanceId, "StageStarted", DateTime.UtcNow.AddMinutes(-2));
        var second = Transition(seeded.InstanceId, "TaskStarted", DateTime.UtcNow.AddMinutes(-1));
        var otherInstance = Transition(Id.New(), "InstanceStarted", DateTime.UtcNow);

        await repository.Create(second);
        await repository.Create(otherInstance);
        await repository.Create(first);

        var instanceTransitions = await repository.GetByInstanceId(seeded.InstanceId);
        var recent = await repository.GetRecent(take: 2);

        Assert.Collection(
            instanceTransitions,
            item => Assert.Equal(first.Id, item.Id),
            item => Assert.Equal(second.Id, item.Id));
        Assert.Collection(
            recent,
            item => Assert.Equal(otherInstance.Id, item.Id),
            item => Assert.Equal(second.Id, item.Id));
    }

    [Fact]
    public async Task CreatePublishesReactiveEventNamesForAllKnownTransitionTypes()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var seeded = await SeedRuntimeExecution(services);
        var repository = services.GetRequiredService<IExecutionTransitionRepository>();
        var expectedNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InstancePromoted"] = RuntimeReactiveEventNames.OrchestrationStarted,
            ["InstanceStarted"] = RuntimeReactiveEventNames.OrchestrationStarted,
            ["InstanceWaitingResponse"] = RuntimeReactiveEventNames.OrchestrationWaiting,
            ["InstanceCompensating"] = RuntimeReactiveEventNames.OrchestrationCompensating,
            ["InstanceCompleted"] = RuntimeReactiveEventNames.OrchestrationCompleted,
            ["InstanceCompensated"] = RuntimeReactiveEventNames.OrchestrationCompensated,
            ["InstanceFailed"] = RuntimeReactiveEventNames.OrchestrationFailed,
            ["StageStarted"] = RuntimeReactiveEventNames.StageStarted,
            ["StageCompleted"] = RuntimeReactiveEventNames.StageCompleted,
            ["StageFailed"] = RuntimeReactiveEventNames.StageFailed,
            ["StageSkipped"] = RuntimeReactiveEventNames.StageSkipped,
            ["TaskStarted"] = RuntimeReactiveEventNames.TaskStarted,
            ["TaskInputTransformed"] = RuntimeReactiveEventNames.TaskInputTransformed,
            ["TaskCompleted"] = RuntimeReactiveEventNames.TaskCompleted,
            ["TaskCallbackCompleted"] = RuntimeReactiveEventNames.TaskCompleted,
            ["TaskFailed"] = RuntimeReactiveEventNames.TaskFailed,
            ["TaskCallbackFailed"] = RuntimeReactiveEventNames.TaskFailed,
            ["TaskSkipped"] = RuntimeReactiveEventNames.TaskSkipped,
            ["TaskWaitingResponse"] = RuntimeReactiveEventNames.TaskWaiting,
            ["TaskRetryScheduled"] = RuntimeReactiveEventNames.TaskRetryScheduled,
            ["TaskRetryStarted"] = RuntimeReactiveEventNames.TaskRetryStarted,
            ["TaskTimedOut"] = RuntimeReactiveEventNames.TaskTimedOut,
            ["DispatchPublished"] = RuntimeReactiveEventNames.DispatchPublished,
            ["TaskDispatched"] = RuntimeReactiveEventNames.DispatchPublished,
            ["DispatchFailed"] = RuntimeReactiveEventNames.DispatchFailed,
            ["CompensationScheduled"] = RuntimeReactiveEventNames.CompensationScheduled,
            ["CompensationStarted"] = RuntimeReactiveEventNames.CompensationStarted,
            ["CompensationDispatched"] = RuntimeReactiveEventNames.CompensationDispatched,
            ["CompensationCompleted"] = RuntimeReactiveEventNames.CompensationCompleted,
            ["CompensationFailed"] = RuntimeReactiveEventNames.CompensationFailed,
            ["ParallelGroupStarted"] = RuntimeReactiveEventNames.ParallelGroupStarted,
            ["ParallelGroupCompleted"] = RuntimeReactiveEventNames.ParallelGroupCompleted,
            ["ParallelGroupFailed"] = RuntimeReactiveEventNames.ParallelGroupFailed,
            ["UnknownTransition"] = RuntimeReactiveEventNames.TransitionRecorded
        };

        foreach (var transition in expectedNames.Keys)
        {
            await repository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = seeded.InstanceId,
                StageExecutionId = seeded.StageId,
                TaskExecutionId = seeded.TaskId,
                TransitionType = transition,
                OccurredOnUtc = DateTime.UtcNow,
                ProducedBy = "tests"
            });
        }

        Assert.Equal(expectedNames.Values.ToArray(), publisher.Events.Select(x => x.EventName).ToArray());
    }

    [Fact]
    public async Task CreateDoesNotPublishReactiveEventWhenInstanceIsUnknown()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExecutionTransitionRepository>();

        await repository.Create(new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            TransitionType = "TaskCompleted",
            OccurredOnUtc = DateTime.UtcNow,
            ProducedBy = "tests"
        });

        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task GetTrafficBucketsActiveStartedCompletedAndFailedInstances()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var now = DateTime.UtcNow;
        await SeedArtifact(services, Id.New());
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Created, now.AddHours(-1)));
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Running, now.AddHours(-1).AddMinutes(-1)));
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Waiting, now.AddHours(-1).AddMinutes(-2)));
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Compensating, now.AddHours(-1).AddMinutes(-3)));
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Completed, now.AddMinutes(-20), completedOnUtc: now.AddMinutes(-5)));
        await instances.Create(Instance(Id.New(), RuntimeOrchestrationArtifactId: Id.New(), OrchestrationInstanceStatus.Failed, now.AddMinutes(-25), failedOnUtc: now.AddMinutes(-3)));
        var repository = services.GetRequiredService<IExecutionTransitionRepository>();

        var traffic = await repository.GetTraffic(now.AddMinutes(-30));

        Assert.Contains(traffic, point => point.Active >= 4);
        Assert.True(traffic.Sum(point => point.Completed) >= 1);
        Assert.True(traffic.Sum(point => point.Failed) >= 1);
    }

    [Fact]
    public async Task GetTrafficUsesHourlyBucketsAndCountsStoppedAndCompensatedTerminalInstances()
    {
        var publisher = new RecordingRuntimeReactiveEventPublisher();
        using var provider = CreateProvider(publisher);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var now = DateTime.UtcNow;
        await SeedArtifact(services, Id.New());
        var instances = services.GetRequiredService<IOrchestrationInstanceRepository>();
        await instances.Create(Instance(
            Id.New(),
            Id.New(),
            OrchestrationInstanceStatus.Stopped,
            now.AddHours(-5),
            stoppedOnUtc: now.AddHours(-1)));
        await instances.Create(Instance(
            Id.New(),
            Id.New(),
            OrchestrationInstanceStatus.Compensated,
            now.AddHours(-4),
            compensatedOnUtc: now.AddMinutes(-30)));
        var repository = services.GetRequiredService<IExecutionTransitionRepository>();

        var traffic = await repository.GetTraffic(now.AddHours(-4));

        Assert.True(traffic.Count >= 4);
        Assert.Contains(traffic, point => point.Active > 0);
        Assert.All(traffic.Zip(traffic.Skip(1)), pair => Assert.Equal(TimeSpan.FromHours(1), pair.Second.BucketUtc - pair.First.BucketUtc));
    }

    private static ServiceProvider CreateProvider(RecordingRuntimeReactiveEventPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Singleton<IRuntimeReactiveEventPublisher>(publisher));
        return services.BuildServiceProvider();
    }

    private static async Task<SeededExecution> SeedRuntimeExecution(IServiceProvider services)
    {
        var artifactId = Id.New();
        var instanceId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();
        var attemptId = Id.New();

        await SeedArtifact(services, artifactId);
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(
            Instance(instanceId, artifactId, OrchestrationInstanceStatus.Running, DateTime.UtcNow.AddMinutes(-1)));
        await services.GetRequiredService<IStageExecutionRepository>().Create(new StageExecution
        {
            Id = stageId,
            OrchestrationInstanceId = instanceId,
            StageKey = "inventory-reservation",
            Order = 1,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await services.GetRequiredService<ITaskExecutionRepository>().Create(new TaskExecution
        {
            Id = taskId,
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageId,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.Running,
            OnErrorPolicy = OnErrorPolicy.Stop,
            StartedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        });
        await services.GetRequiredService<ITaskExecutionAttemptRepository>().Create(new TaskExecutionAttempt
        {
            Id = attemptId,
            TaskExecutionId = taskId,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = DateTime.UtcNow.AddSeconds(-30)
        });

        return new SeededExecution(instanceId, stageId, taskId, attemptId);
    }

    private static async Task SeedArtifact(IServiceProvider services, Id artifactId)
    {
        await services.GetRequiredService<IRuntimeArtifactRepository>().Upsert(new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 2, 3),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        });
    }

    private static OrchestrationInstance Instance(
        Id instanceId,
        Id RuntimeOrchestrationArtifactId,
        OrchestrationInstanceStatus status,
        DateTime startedOnUtc,
        DateTime? completedOnUtc = null,
        DateTime? failedOnUtc = null,
        DateTime? stoppedOnUtc = null,
        DateTime? compensatedOnUtc = null)
        => new()
        {
            Id = instanceId,
            RuntimeOrchestrationArtifactId = RuntimeOrchestrationArtifactId,
            TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created:correlation-1",
            Status = status,
            StartedOnUtc = startedOnUtc,
            LastUpdatedOnUtc = completedOnUtc ?? failedOnUtc ?? startedOnUtc,
            CompletedOnUtc = completedOnUtc,
            FailedOnUtc = failedOnUtc,
            StoppedOnUtc = stoppedOnUtc,
            CompensatedOnUtc = compensatedOnUtc,
            SnapshotPayload = JsonNode.Parse("{}")
        };

    private static ExecutionTransition Transition(Id instanceId, string type, DateTime occurredOnUtc)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            TransitionType = type,
            OccurredOnUtc = occurredOnUtc,
            ProducedBy = "tests"
        };

    private sealed record SeededExecution(Id InstanceId, Id StageId, Id TaskId, Id AttemptId);

    private sealed class RecordingRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
    {
        public List<RuntimeReactiveEvent> Events { get; } = [];

        public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        {
            Events.Add(eventData);
            return Task.CompletedTask;
        }
    }
}
