namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using System.Text.Json.Nodes;
using NSubstitute;

public sealed class EnterIdleInstanceDecisionHandlerTests
{
    [Fact]
    public async Task HandleAsync_EntersTheFixedTwentyMinuteReconciliationWindowAndCancelsResidualDispatches()
    {
        var instance = Instance(OrchestrationInstanceStatus.Running);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        var transitions = Substitute.For<IExecutionTransitionRepository>();
        var cancellations = Substitute.For<IOrchestrationDispatchCancellationCoordinator>();
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        instances.Update(instance, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        transitions.Create(Arg.Any<ExecutionTransition>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        cancellations.CancelPendingAsync(instance, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        var handler = new EnterIdleInstanceDecisionHandler(instances, transitions, cancellations);
        var before = DateTime.UtcNow;

        await handler.HandleAsync(new EnterIdleInstanceDecision(instance.Id, Id.New(), Id.New(), "all retry attempts were exhausted"));

        Assert.Equal(OrchestrationInstanceStatus.Idle, instance.Status);
        Assert.Equal("all retry attempts were exhausted", instance.ErrorSummary);
        Assert.True(instance.DispatchCancellationPending);
        Assert.NotNull(instance.IdleSinceUtc);
        Assert.NotNull(instance.IdleExpiresOnUtc);
        Assert.InRange(instance.IdleExpiresOnUtc!.Value, before.AddMinutes(20).AddSeconds(-1), before.AddMinutes(20).AddSeconds(1));
        await transitions.Received(1).Create(
            Arg.Is<ExecutionTransition>(transition =>
                transition.TransitionType == "InstanceIdle" &&
                transition.FromStatus == OrchestrationInstanceStatus.Running.ToString() &&
                transition.ToStatus == OrchestrationInstanceStatus.Idle.ToString()),
            Arg.Any<CancellationToken>());
        await cancellations.Received(1).CancelPendingAsync(
            instance,
            Arg.Is<string>(reason => reason.Contains("reconciliation idle", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OrchestrationInstanceStatus.Idle)]
    [InlineData(OrchestrationInstanceStatus.Completed)]
    [InlineData(OrchestrationInstanceStatus.DeadLettered)]
    [InlineData(OrchestrationInstanceStatus.Failed)]
    public async Task HandleAsync_DoesNotReenterAnIdleOrTerminalInstance(OrchestrationInstanceStatus status)
    {
        var instance = Instance(status);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        var transitions = Substitute.For<IExecutionTransitionRepository>();
        var cancellations = Substitute.For<IOrchestrationDispatchCancellationCoordinator>();
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var handler = new EnterIdleInstanceDecisionHandler(instances, transitions, cancellations);

        await handler.HandleAsync(new EnterIdleInstanceDecision(instance.Id, null, null, "ignored"));

        await instances.DidNotReceiveWithAnyArgs().Update(default!, default);
        await transitions.DidNotReceiveWithAnyArgs().Create(default!, default);
        await cancellations.DidNotReceiveWithAnyArgs().CancelPendingAsync(default!, default!, default);
    }

    [Fact]
    public async Task LifetimeExpirationDoesNotRenewIdleWindowWhenCallbackResumesTheSameExpiredRun()
    {
        var instance = Instance(OrchestrationInstanceStatus.Running);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var handler = new EnterIdleInstanceDecisionHandler(instances,
            Substitute.For<IExecutionTransitionRepository>(), Substitute.For<IOrchestrationDispatchCancellationCoordinator>());
        var anchor = DateTime.UtcNow.AddDays(-3);
        var decision = new EnterIdleInstanceDecision(instance.Id, null, null, "lifetime expired") { LifetimeAnchorUtc = anchor };

        await handler.HandleAsync(decision);
        var originalDeadline = instance.IdleExpiresOnUtc;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.IdleExpiresOnUtc = null;
        await handler.HandleAsync(decision);

        Assert.Equal(originalDeadline, instance.IdleExpiresOnUtc);
    }

    [Fact]
    public async Task IdleWindowUsesTheVersionPolicy()
    {
        var instance = Instance(OrchestrationInstanceStatus.Running);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var runtimeArtifact = new RuntimeOrchestrationArtifact { Id = instance.RuntimeOrchestrationArtifactId,
            OrchestrationDefinitionKey = "orders", ArtifactType = "new", Version = new(1, 0, 0),
            ArtifactChecksum = new("hash"), ArtifactPayload = new JsonObject(), IsActive = true };
        var definition = new OrchestrationArtifact(Id.New(), Id.New(), "orders", "Orders", "sales",
            new(1, 0, 0), new("hash"), [], [], []) { IdleDurationSeconds = 3600 };
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResolvedOrchestrationArtifact { RuntimeArtifact = runtimeArtifact, Artifact = definition });
        var handler = new EnterIdleInstanceDecisionHandler(instances, Substitute.For<IExecutionTransitionRepository>(),
            Substitute.For<IOrchestrationDispatchCancellationCoordinator>(), artifacts: resolver);
        var before = DateTime.UtcNow;

        await handler.HandleAsync(new EnterIdleInstanceDecision(instance.Id, null, null, "timeout"));

        Assert.InRange(instance.IdleExpiresOnUtc!.Value, before.AddSeconds(3600), DateTime.UtcNow.AddSeconds(3600));
    }

    [Fact]
    public async Task PreResolvedLifetimePolicyAvoidsReadingTheArtifactAgain()
    {
        var instance = Instance(OrchestrationInstanceStatus.Running);
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        instances.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var resolver = Substitute.For<IRuntimeArtifactResolver>();
        var handler = new EnterIdleInstanceDecisionHandler(instances, Substitute.For<IExecutionTransitionRepository>(),
            Substitute.For<IOrchestrationDispatchCancellationCoordinator>(), artifacts: resolver);

        await handler.HandleAsync(new EnterIdleInstanceDecision(instance.Id, null, null, "lifetime") { IdleDurationSeconds = 300 });

        await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
        Assert.NotNull(instance.IdleExpiresOnUtc);
    }

    [Fact]
    public void ConstructorRejectsRequiredDependencies()
    {
        var instances = Substitute.For<IOrchestrationInstanceRepository>();
        var transitions = Substitute.For<IExecutionTransitionRepository>();
        var cancellations = Substitute.For<IOrchestrationDispatchCancellationCoordinator>();

        Assert.Throws<ArgumentNullException>(() => new EnterIdleInstanceDecisionHandler(null!, transitions, cancellations));
        Assert.Throws<ArgumentNullException>(() => new EnterIdleInstanceDecisionHandler(instances, null!, cancellations));
        Assert.Throws<ArgumentNullException>(() => new EnterIdleInstanceDecisionHandler(instances, transitions, null!));
    }

    private static OrchestrationInstance Instance(OrchestrationInstanceStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders.create",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = status,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
}

public sealed class DefaultOrchestrationIdleProcessorTests
{
    [Fact]
    public async Task ProcessExpiredAsync_DeadLettersOnlyInstancesWhoseIdleWindowHasElapsed()
    {
        var candidate = Instance(OrchestrationInstanceStatus.Idle);
        candidate.IdleExpiresOnUtc = DateTime.UtcNow.AddMinutes(-1);
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationIdleInstanceRepository>();
        var idleRepository = (IOrchestrationIdleInstanceRepository)repository;
        idleRepository.GetIdleExpiringOnOrBefore(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([candidate]));
        repository.GetById(candidate.Id, Arg.Any<CancellationToken>()).Returns(candidate);
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(async call =>
            {
                await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>());
                return true;
            });
        var deadLetter = Substitute.For<IDecisionHandler<DeadLetterInstanceDecision>>();
        deadLetter.HandleAsync(Arg.Any<DeadLetterInstanceDecision>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var processor = new DefaultOrchestrationIdleProcessor(repository, coordinator, deadLetter);

        var processed = await processor.ProcessExpiredAsync(DateTime.UtcNow, take: 10);

        Assert.Equal(1, processed);
        await deadLetter.Received(1).HandleAsync(
            Arg.Is<DeadLetterInstanceDecision>(decision =>
                decision.InstanceId == candidate.Id &&
                decision.Reason.Contains("idle window expired", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessExpiredAsync_IgnoresAConcurrentCallbackThatResumedTheInstance()
    {
        var candidate = Instance(OrchestrationInstanceStatus.Idle);
        candidate.IdleExpiresOnUtc = DateTime.UtcNow.AddMinutes(-1);
        var resumed = Instance(OrchestrationInstanceStatus.Running);
        resumed.Id = candidate.Id;
        var repository = Substitute.For<IOrchestrationInstanceRepository, IOrchestrationIdleInstanceRepository>();
        var idleRepository = (IOrchestrationIdleInstanceRepository)repository;
        idleRepository.GetIdleExpiringOnOrBefore(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>([candidate]));
        repository.GetById(candidate.Id, Arg.Any<CancellationToken>()).Returns(resumed);
        var coordinator = Substitute.For<IOrchestrationInstanceCoordinator>();
        coordinator.TryExecuteAsync(Arg.Any<Id>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(async call =>
            {
                await call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>());
                return true;
            });
        var deadLetter = Substitute.For<IDecisionHandler<DeadLetterInstanceDecision>>();
        var processor = new DefaultOrchestrationIdleProcessor(repository, coordinator, deadLetter);

        var processed = await processor.ProcessExpiredAsync(DateTime.UtcNow, take: 10);

        Assert.Equal(0, processed);
        await deadLetter.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }

    [Fact]
    public async Task ProcessExpiredAsync_ReturnsZeroWhenStorageDoesNotSupportIdleQueries()
    {
        var repository = Substitute.For<IOrchestrationInstanceRepository>();
        var processor = new DefaultOrchestrationIdleProcessor(
            repository,
            Substitute.For<IOrchestrationInstanceCoordinator>(),
            Substitute.For<IDecisionHandler<DeadLetterInstanceDecision>>());

        Assert.Equal(0, await processor.ProcessExpiredAsync(DateTime.UtcNow));
    }

    private static OrchestrationInstance Instance(OrchestrationInstanceStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "orders.create",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = status,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
}
