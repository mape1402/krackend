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
    public async Task GlobalPagingDoesNotSkipExpiredRowsWhenOtherPoliciesLeaveRowsRunning()
    {
        var now = DateTime.UtcNow;
        var fastId = Id.New();
        var slowId = Id.New();
        var instancesData = Enumerable.Range(0, 5).Select(i => Instance(i is 1 or 3 ? slowId : fastId,
            now.AddHours(-2).AddSeconds(i))).ToArray();
        var artifacts = Substitute.For<IRuntimeArtifactRepository, IRuntimeArtifactLifetimePolicyRepository>();
        ((IRuntimeArtifactLifetimePolicyRepository)artifacts).GetLifetimePolicyPage(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>>(
                new[] { new RuntimeArtifactLifetimePolicyItem(fastId, new JsonObject { ["max"] = 60 }),
                        new RuntimeArtifactLifetimePolicyItem(slowId, new JsonObject { ["max"] = 86400 }) }
                    .Skip(call.ArgAt<int>(0)).Take(call.ArgAt<int>(1)).ToArray()));
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(call => new OrchestrationArtifact(
            Id.New(), Id.New(), "orders", "Orders", "sales", new(1, 0, 0), new("hash"), [], [], [])
            { MaxInstanceDurationSeconds = JsonNode.Parse(call.Arg<string>())!["max"]!.GetValue<int>() });
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationLifetimeScanRepository>();
        repository.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => instancesData.Single(x => x.Id == call.Arg<Id>()));
        var scan = (IOrchestrationLifetimeScanRepository)repository;
        scan.GetLifetimeCandidatePage(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<OrchestrationLifetimeCandidate>>(instancesData
                .Where(x => x.Status == OrchestrationInstanceStatus.Running && x.StartedOnUtc <= call.ArgAt<DateTime>(0))
                .OrderBy(x => x.StartedOnUtc).ThenBy(x => x.Id).Skip(call.ArgAt<int>(1)).Take(call.ArgAt<int>(2))
                .Select(x => new OrchestrationLifetimeCandidate(x.Id, x.RuntimeOrchestrationArtifactId, x.StartedOnUtc)).ToArray()));
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var idle = Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>();
        idle.HandleAsync(Arg.Any<EnterIdleInstanceDecision>(), Arg.Any<CancellationToken>())
            .Returns(call => { instancesData.Single(x => x.Id == call.Arg<EnterIdleInstanceDecision>().InstanceId).Status = OrchestrationInstanceStatus.Idle; return Task.CompletedTask; });
        var processor = new OrchestrationLifetimeProcessor(artifacts, serializer, repository, coordinator, idle,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 2 }), NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(3, await processor.ProcessAsync(now));
        Assert.Equal(2, instancesData.Count(x => x.Status == OrchestrationInstanceStatus.Running));
        Assert.All(instancesData.Where(x => x.RuntimeOrchestrationArtifactId == fastId), x => Assert.Equal(OrchestrationInstanceStatus.Idle, x.Status));
    }

    [Fact]
    public async Task GlobalScanMatchesPoliciesWithoutQueryingPerArtifactOrLockingUnexpiredInstances()
    {
        var now = DateTime.UtcNow;
        var shortId = Id.New();
        var longId = Id.New();
        var disabledId = Id.New();
        var old = new[] { Instance(shortId, now.AddHours(-2)), Instance(longId, now.AddHours(-2)), Instance(disabledId, now.AddHours(-2)) };
        var artifacts = Substitute.For<IRuntimeArtifactRepository, IRuntimeArtifactLifetimePolicyRepository>();
        ((IRuntimeArtifactLifetimePolicyRepository)artifacts).GetLifetimePolicyPage(0, 10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>>([
                new(shortId, new JsonObject { ["max"] = 60 }),
                new(longId, new JsonObject { ["max"] = 86400 }),
                new(disabledId, new JsonObject { ["max"] = 0 })]));
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(call => new OrchestrationArtifact(
            Id.New(), Id.New(), "orders", "Orders", "sales", new(1, 0, 0), new("hash"), [], [], [])
            { MaxInstanceDurationSeconds = JsonNode.Parse(call.Arg<string>())!["max"]!.GetValue<int>() });
        var instances = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationLifetimeScanRepository, IOrchestrationLifetimeCandidateRepository>();
        var scan = (IOrchestrationLifetimeScanRepository)instances;
        scan.GetLifetimeCandidatePage(Arg.Any<DateTime>(), 0, 10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationLifetimeCandidate>>(old.Select(x =>
                new OrchestrationLifetimeCandidate(x.Id, x.RuntimeOrchestrationArtifactId, x.StartedOnUtc)).ToArray()));
        instances.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(call => old.Single(x => x.Id == call.Arg<Id>()));
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var idle = Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>();
        var processor = new OrchestrationLifetimeProcessor(artifacts, serializer, instances, coordinator, idle,
            Options.Create(new OrchestrationTimeoutOptions { InstanceLifetimeBatchSize = 10 }), NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessAsync(now));
        await scan.Received(1).GetLifetimeCandidatePage(now.AddSeconds(-60), 0, 10, Arg.Any<CancellationToken>());
        await ((IOrchestrationLifetimeCandidateRepository)instances).DidNotReceiveWithAnyArgs()
            .GetLifetimeCandidates(default, default, default, default, default);
        await instances.Received(1).GetById(old[0].Id, Arg.Any<CancellationToken>());
        await instances.DidNotReceive().GetById(old[1].Id, Arg.Any<CancellationToken>());
        await instances.DidNotReceive().GetById(old[2].Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisabledVersionDoesNotScanOrExpireItsInstances()
    {
        var artifact = new RuntimeOrchestrationArtifact { Id = Id.New(), OrchestrationDefinitionKey = "orders", ArtifactType = "new",
            Version = new(1, 0, 0), ArtifactChecksum = new("hash"), ArtifactPayload = new JsonObject(), IsActive = true };
        var artifacts = Substitute.For<IRuntimeArtifactRepository, IRuntimeArtifactLifetimePolicyRepository>();
        ((IRuntimeArtifactLifetimePolicyRepository)artifacts).GetLifetimePolicyPage(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>>([new(artifact.Id, artifact.ArtifactPayload)]));
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(new OrchestrationArtifact(
            Id.New(), Id.New(), "orders", "Orders", "sales", new(1, 0, 0), new("hash"), [], [], []) { MaxInstanceDurationSeconds = 0 });
        var instances = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationLifetimeCandidateRepository>();
        var processor = new OrchestrationLifetimeProcessor(artifacts, serializer, instances,
            Substitute.For<IOrchestrationInstanceCoordinator>(), Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>(),
            Options.Create(new OrchestrationTimeoutOptions()), NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(0, await processor.ProcessAsync(DateTime.UtcNow));
        await ((IOrchestrationLifetimeCandidateRepository)instances).DidNotReceiveWithAnyArgs()
            .GetLifetimeCandidates(default, default, default, default, default);
    }

    [Fact]
    public async Task ScanExpiresOldInstancesInPagesWithoutExpiringRecentlyRecoveredOnes()
    {
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var artifact = new RuntimeOrchestrationArtifact
        {
            Id = artifactId, OrchestrationDefinitionKey = "orders", ArtifactType = "new", Version = new(1, 0, 0),
            ArtifactChecksum = new("hash"), ArtifactPayload = new JsonObject(), IsActive = true
        };
        var old = Enumerable.Range(0, 5).Select(i => Instance(artifactId, now.AddDays(-5).AddMinutes(i))).ToArray();
        old[0].Metadata["Recovery.ReplayRequestedOnUtc"] = JsonValue.Create(now.AddMinutes(-5));
        var instances = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationLifetimeCandidateRepository>();
        var candidates = (IOrchestrationLifetimeCandidateRepository)instances;
        instances.GetById(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(call => old.Single(x => x.Id == call.Arg<Id>()));
        candidates.GetLifetimeCandidates(artifactId, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyCollection<Id>>(old
                .Where(x => x.Status == OrchestrationInstanceStatus.Running && x.StartedOnUtc <= call.ArgAt<DateTime>(1))
                .OrderBy(x => x.StartedOnUtc).ThenBy(x => x.Id).Skip(call.ArgAt<int>(2)).Take(call.ArgAt<int>(3)).Select(x => x.Id).ToArray()));
        var artifacts = Substitute.For<IRuntimeArtifactRepository, IRuntimeArtifactLifetimePolicyRepository>();
        ((IRuntimeArtifactLifetimePolicyRepository)artifacts).GetLifetimePolicyPage(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>>([new(artifact.Id, artifact.ArtifactPayload)]));
        var serializer = Substitute.For<IRuntimeArtifactSerializer>();
        serializer.Deserialize(Arg.Any<string>()).Returns(new OrchestrationArtifact(
            Id.New(), Id.New(), "orders", "Orders", "sales", new(1, 0, 0), new("hash"), [], [], []));
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), false)
            .Returns(async call => { await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()); return true; });
        var idle = Substitute.For<IDecisionHandler<EnterIdleInstanceDecision>>();
        idle.HandleAsync(Arg.Any<EnterIdleInstanceDecision>(), Arg.Any<CancellationToken>())
            .Returns(call => { old.Single(x => x.Id == call.Arg<EnterIdleInstanceDecision>().InstanceId).Status = OrchestrationInstanceStatus.Idle; return Task.CompletedTask; });
        var processor = new OrchestrationLifetimeProcessor(artifacts, serializer, instances, coordinator, idle,
            Options.Create(new OrchestrationTimeoutOptions { DefaultInstanceLifetimeSeconds = 86400, InstanceLifetimeBatchSize = 2 }),
            NullLogger<OrchestrationLifetimeProcessor>.Instance);

        Assert.Equal(4, await processor.ProcessAsync(now));
        Assert.Equal(OrchestrationInstanceStatus.Running, old[0].Status);
        Assert.All(old.Skip(1), x => Assert.Equal(OrchestrationInstanceStatus.Idle, x.Status));
        await ((IRuntimeArtifactLifetimePolicyRepository)artifacts).Received(1)
            .GetLifetimePolicyPage(0, 2, Arg.Any<CancellationToken>());
        await artifacts.DidNotReceiveWithAnyArgs().GetAll(default);
        await artifacts.DidNotReceiveWithAnyArgs().GetById(default, default);
    }

    private static OrchestrationInstance Instance(Id artifactId, DateTime started)
        => new() { Id = Id.New(), RuntimeOrchestrationArtifactId = artifactId, OrchestrationDefinitionKey = "orders",
            CorrelationId = "correlation", SagaId = "saga", ExecutionKey = "execution", StartedOnUtc = started,
            LastUpdatedOnUtc = started, Status = OrchestrationInstanceStatus.Running };
}
