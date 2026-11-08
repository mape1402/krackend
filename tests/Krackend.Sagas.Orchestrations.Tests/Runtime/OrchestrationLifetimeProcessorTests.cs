namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public sealed class OrchestrationLifetimeProcessorTests
{
    [Fact]
    public void DisabledExpirationIsProjectedWithoutADeadline()
    {
        var instance = Instance(null);
        var artifact = new OrchestrationArtifact(Id.New(), Id.New(), "orders", "Orders", "sales",
            new(1, 0, 0), new("hash"), [], [], []) { MaxInstanceDurationSeconds = 0, IdleDurationSeconds = 300 };
        InstanceLifetimePolicy.Project(instance, artifact, new OrchestrationTimeoutOptions(), DateTime.UtcNow);

        Assert.Equal(0, instance.MaxInstanceDurationSeconds);
        Assert.Equal(300, instance.IdleDurationSeconds);
        Assert.Null(instance.ExpiresOnUtc);
        InstanceLifetimePolicy.ResetDeadline(instance, DateTime.UtcNow.AddHours(1));
        Assert.Null(instance.ExpiresOnUtc);
    }

    [Fact]
    public async Task ScansOnlyProjectedDeadlinesAndContinuesPastBusyCandidates()
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, 5).Select(i => Instance(now.AddMinutes(-i - 1))).ToArray();
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationExpirationRepository>();
        var expiration = (IOrchestrationExpirationRepository)repository;
        expiration.GetExpiredInstances(now, Arg.Any<int>(), 2, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<Id>>(rows.Where(x => x.Status == OrchestrationInstanceStatus.Running)
                .OrderBy(x => x.ExpiresOnUtc).ThenBy(x => x.Id).Skip(call.ArgAt<int>(1)).Take(2).Select(x => x.Id).ToArray()));
        repository.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => rows.Single(x => x.Id == call.Arg<Id>()));
        var busy = rows[4].Id;
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { if (call.Arg<Id>() == busy) return false; await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var idle = Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>();
        idle.HandleAsync(Arg.Any<EnterIdleInstanceDecision>(), Arg.Any<CancellationToken>())
            .Returns(call => { rows.Single(x => x.Id == call.Arg<EnterIdleInstanceDecision>().InstanceId).Status = OrchestrationInstanceStatus.Idle; return Task.CompletedTask; });
        var processor = new OrchestrationLifetimeProcessor(repository, coordinator, idle,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 2 }), NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(4, await processor.ProcessAsync(now));
        Assert.Equal(OrchestrationInstanceStatus.Running, rows.Single(x => x.Id == busy).Status);
        await idle.Received(4).HandleAsync(Arg.Is<EnterIdleInstanceDecision>(x => x.IdleDurationSeconds == 1200), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContinuesPagingWhenAnIdleTransitionCommitsButCancellationFails()
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, 3).Select(i => Instance(now.AddMinutes(-i - 1))).ToArray();
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationExpirationRepository>();
        ((IOrchestrationExpirationRepository)repository).GetExpiredInstances(now, Arg.Any<int>(), 2, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<Id>>(rows.Where(x => x.Status == OrchestrationInstanceStatus.Running)
                .OrderBy(x => x.ExpiresOnUtc).ThenBy(x => x.Id).Skip(call.ArgAt<int>(1)).Take(2).Select(x => x.Id).ToArray()));
        repository.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => rows.Single(x => x.Id == call.Arg<Id>()));
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var idle = Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>();
        idle.HandleAsync(Arg.Any<EnterIdleInstanceDecision>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var row = rows.Single(x => x.Id == call.Arg<EnterIdleInstanceDecision>().InstanceId);
            row.Status = OrchestrationInstanceStatus.Idle;
            if (row == rows[2]) throw new InvalidOperationException("cancellation failed after commit");
            return Task.CompletedTask;
        });
        var processor = new OrchestrationLifetimeProcessor(repository, coordinator, idle,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 2 }), NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(2, await processor.ProcessAsync(now));
        Assert.All(rows, row => Assert.Equal(OrchestrationInstanceStatus.Idle, row.Status));
    }

    [Fact]
    public async Task BackfillRetriesUnavailableArtifactsWithoutQueryingThemPerInstance()
    {
        var artifactId = Id.New();
        var rows = new[] { Instance(null), Instance(null) };
        foreach (var row in rows) { row.RuntimeOrchestrationArtifactId = artifactId; row.MaxInstanceDurationSeconds = null; }
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationExpirationRepository>();
        ((IOrchestrationExpirationRepository)repository).GetUnprojectedInstances(0, 100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<Id>>(rows.Select(x => x.Id).ToArray()));
        repository.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => rows.Single(x => x.Id == call.Arg<Id>()));
        var artifacts = Substitute.For<IRuntimeArtifactRepository>();
        artifacts.GetById(artifactId, Arg.Any<CancellationToken>()).Returns<Task<RuntimeOrchestrationArtifact>>(_ => throw new KeyNotFoundException());
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var backfill = new OrchestrationLifetimeBackfill(repository, artifacts, Substitute.For<IRuntimeArtifactSerializer>(), coordinator,
            Options.Create(new OrchestrationTimeoutOptions()), NullLogger<OrchestrationLifetimeBackfill>.Instance);

        Assert.False(await backfill.ProcessAsync());
        await artifacts.Received(1).GetById(artifactId, Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().Update(default!, default);
    }

    [Fact]
    public async Task BackfillReadsEachArtifactOnceAndPreservesReplayAnchor()
    {
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var rows = new[] { Instance(null), Instance(null) };
        foreach (var row in rows) { row.RuntimeOrchestrationArtifactId = artifactId; row.MaxInstanceDurationSeconds = null; }
        rows[0].Metadata["Recovery.ReplayRequestedOnUtc"] = JsonValue.Create(now.AddHours(-1));
        rows[0].Metadata["Lifetime.ExpirationAnchorOnUtc"] = JsonValue.Create(now.AddHours(-1));
        rows[0].Metadata["Lifetime.IdleDeadlineOnUtc"] = JsonValue.Create(now.AddMinutes(10));
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationExpirationRepository>();
        ((IOrchestrationExpirationRepository)repository).GetUnprojectedInstances(Arg.Any<int>(), 100, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<Id>>(rows.Where(x => x.MaxInstanceDurationSeconds is null)
                .Skip(call.ArgAt<int>(0)).Select(x => x.Id).ToArray()));
        repository.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => rows.Single(x => x.Id == call.Arg<Id>()));
        var artifacts = Substitute.For<IRuntimeArtifactRepository>();
        artifacts.GetById(artifactId, Arg.Any<CancellationToken>()).Returns(new RuntimeOrchestrationArtifact
        {
            Id = artifactId, OrchestrationDefinitionKey = "orders", ArtifactType = "new", Version = new(1, 0, 0),
            ArtifactChecksum = new("hash"), ArtifactPayload = new JsonObject()
        });
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(new OrchestrationArtifact(Id.New(), Id.New(), "orders", "Orders", "sales",
            new(1, 0, 0), new("hash"), [], [], []) { MaxInstanceDurationSeconds = 7200, IdleDurationSeconds = 300 });
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var backfill = new OrchestrationLifetimeBackfill(repository, artifacts, serializer, coordinator,
            Options.Create(new OrchestrationTimeoutOptions()), NullLogger<OrchestrationLifetimeBackfill>.Instance);

        Assert.True(await backfill.ProcessAsync());
        Assert.Equal(now.AddHours(1), rows[0].ExpiresOnUtc);
        Assert.Equal(rows[0].ExpiresOnUtc, rows[0].Metadata["Lifetime.ExpirationAnchorOnUtc"]!.GetValue<DateTime>());
        Assert.Equal(rows[1].StartedOnUtc.AddHours(2), rows[1].ExpiresOnUtc);
        Assert.All(rows, row => Assert.Equal(300, row.IdleDurationSeconds));
        await artifacts.Received(1).GetById(artifactId, Arg.Any<CancellationToken>());
    }

    private static OrchestrationInstance Instance(DateTime? expiry)
        => new() { Id = Id.New(), RuntimeOrchestrationArtifactId = Id.New(), OrchestrationDefinitionKey = "orders",
            CorrelationId = "correlation", SagaId = "saga", ExecutionKey = "execution", StartedOnUtc = DateTime.UtcNow.AddDays(-2),
            LastUpdatedOnUtc = DateTime.UtcNow, Status = OrchestrationInstanceStatus.Running,
            MaxInstanceDurationSeconds = 86400, IdleDurationSeconds = 1200, ExpiresOnUtc = expiry };
}
