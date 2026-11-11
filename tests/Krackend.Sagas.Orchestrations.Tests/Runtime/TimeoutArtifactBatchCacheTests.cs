using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class TimeoutArtifactBatchCacheTests
{
    [Fact]
    public async Task ResolverReusesBatchForEngineReadsAndChecksStateAgainOutsideThePass()
    {
        var artifactId = Id.New();
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        var stored = new RuntimeOrchestrationArtifact
        {
            Id = artifactId, IsActive = true, OrchestrationDefinitionKey = "key", ArtifactType = "test",
            Version = new(1, 0, 0), ArtifactChecksum = new("checksum"),
            ArtifactPayload = System.Text.Json.Nodes.JsonNode.Parse("{}")!
        };
        repository.GetById(artifactId, Arg.Any<CancellationToken>()).Returns(stored);
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(new OrchestrationArtifact(Id.New(), Id.New(), "key", "name", "description",
            new(1, 0, 0), new("checksum"), [], [], []));
        var pass = new OrchestrationTimeoutBatchContext();
        var firstResolver = new DefaultRuntimeArtifactResolver(repository, serializer) { TimeoutBatch = pass };
        var secondResolver = new DefaultRuntimeArtifactResolver(repository, serializer) { TimeoutBatch = pass };

        var first = await firstResolver.ResolveAsync(artifactId.ToString());
        Assert.Same(first, await secondResolver.ResolveAsync(artifactId.ToString()));
        await repository.Received(1).GetById(artifactId, Arg.Any<CancellationToken>());
        stored.IsActive = false;
        secondResolver.TimeoutBatch = null!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondResolver.ResolveAsync(artifactId.ToString()));
        await repository.Received(2).GetById(artifactId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TimeoutProcessorsInSeparateScopesShareTheArtifactForOneRepairPass()
    {
        var artifactId = Id.New();
        var instanceId = Id.New();
        var stageId = Id.New();
        var firstTask = WaitingTask(instanceId, stageId);
        var secondTask = WaitingTask(instanceId, stageId);
        var tasks = Substitute.For<ITaskExecutionRepository>();
        tasks.GetById(firstTask.Id, Arg.Any<CancellationToken>()).Returns(firstTask);
        tasks.GetById(secondTask.Id, Arg.Any<CancellationToken>()).Returns(secondTask);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        instances.GetById(instanceId, Arg.Any<CancellationToken>()).Returns(new OrchestrationInstance
        {
            Id = instanceId, RuntimeOrchestrationArtifactId = artifactId, Status = OrchestrationInstanceStatus.Running,
            OrchestrationDefinitionKey = "key", CorrelationId = "correlation", SagaId = "saga", ExecutionKey = "execution"
        });
        var stages = Substitute.For<IStageExecutionRepository>();
        stages.GetById(stageId, Arg.Any<CancellationToken>()).Returns(new StageExecution { Id = stageId, StageKey = "stage" });
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(async call => { await call.ArgAt<Func<CancellationToken, Task>>(1)(call.ArgAt<CancellationToken>(2)); return true; });
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        resolver.ResolveAsync(artifactId.ToString(), Arg.Any<CancellationToken>()).Returns(new ResolvedOrchestrationArtifact
        {
            RuntimeArtifact = null!,
            Artifact = new OrchestrationArtifact(Id.New(), Id.New(), "key", "name", "description",
                new(1, 0, 0), new("checksum"), [], [], [])
        });
        DefaultOrchestrationTimeoutProcessor Processor() => new(instances, stages, tasks,
            Substitute.For<ITaskExecutionAttemptRepository>(), Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<IExecutionTransitionRepository>(), resolver, Substitute.For<ISagaEngine>(), coordinator,
            Substitute.For<IRuntimeAdmissionController>(), Substitute.For<IRuntimeStorageUnitOfWork>());

        var batch = new OrchestrationTimeoutBatchContext();
        await Processor().ProcessTaskTimeoutAsync(firstTask.Id, DateTime.UtcNow, batch);
        await Processor().ProcessTaskTimeoutAsync(secondTask.Id, DateTime.UtcNow, batch);
        await resolver.Received(1).ResolveAsync(artifactId.ToString(), Arg.Any<CancellationToken>());
        await Processor().ProcessTaskTimeoutAsync(firstTask.Id, DateTime.UtcNow, new OrchestrationTimeoutBatchContext());
        await resolver.Received(2).ResolveAsync(artifactId.ToString(), Arg.Any<CancellationToken>());
    }

    private static TaskExecution WaitingTask(Id instanceId, Id stageId) => new()
    {
        Id = Id.New(), OrchestrationInstanceId = instanceId, StageExecutionId = stageId,
        TaskKey = "task", Status = TaskExecutionStatus.WaitingResponse, WaitingSinceUtc = DateTime.UtcNow.AddMinutes(-1)
    };

    [Fact]
    public async Task EachArtifactIsReadOncePerPassAndTheNextPassReadsFreshData()
    {
        var id = Id.New();
        var otherId = Id.New();
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        var first = Artifact();
        var updated = Artifact();
        var other = Artifact();
        resolver.ResolveAsync(id.ToString(), Arg.Any<CancellationToken>()).Returns(first, updated);
        resolver.ResolveAsync(otherId.ToString(), Arg.Any<CancellationToken>()).Returns(other);
        var pass = new OrchestrationTimeoutBatchContext();

        Assert.Same(first, await pass.ResolveAsync(id, resolver, default));
        Assert.Same(first, await pass.ResolveAsync(id, resolver, default));
        Assert.Same(other, await pass.ResolveAsync(otherId, resolver, default));
        Assert.Same(updated, await new OrchestrationTimeoutBatchContext().ResolveAsync(id, resolver, default));

        await resolver.Received(2).ResolveAsync(id.ToString(), Arg.Any<CancellationToken>());
        await resolver.Received(1).ResolveAsync(otherId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingArtifactIsRetriedOnNextPassAndOtherFailuresAreNotCached()
    {
        var missing = Id.New();
        var transient = Id.New();
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        resolver.ResolveAsync(missing.ToString(), Arg.Any<CancellationToken>())
            .Returns<Task<ResolvedOrchestrationArtifact>>(_ => throw new KeyNotFoundException());
        resolver.ResolveAsync(transient.ToString(), Arg.Any<CancellationToken>())
            .Returns<Task<ResolvedOrchestrationArtifact>>(_ => throw new InvalidOperationException());
        var pass = new OrchestrationTimeoutBatchContext();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => pass.ResolveAsync(missing, resolver, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => pass.ResolveAsync(missing, resolver, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new OrchestrationTimeoutBatchContext().ResolveAsync(missing, resolver, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pass.ResolveAsync(transient, resolver, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pass.ResolveAsync(transient, resolver, default));

        await resolver.Received(2).ResolveAsync(missing.ToString(), Arg.Any<CancellationToken>());
        await resolver.Received(2).ResolveAsync(transient.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancellationIsRespectedEvenForCachedArtifact()
    {
        var id = Id.New();
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        resolver.ResolveAsync(id.ToString(), Arg.Any<CancellationToken>()).Returns(Artifact());
        var pass = new OrchestrationTimeoutBatchContext();
        await pass.ResolveAsync(id, resolver, default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass.ResolveAsync(id, resolver, cancellation.Token));
        await resolver.Received(1).ResolveAsync(id.ToString(), Arg.Any<CancellationToken>());
    }

    private static ResolvedOrchestrationArtifact Artifact() => new() { Artifact = null!, RuntimeArtifact = null! };
}
