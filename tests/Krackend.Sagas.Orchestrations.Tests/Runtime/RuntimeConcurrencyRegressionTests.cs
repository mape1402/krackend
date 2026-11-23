using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeConcurrencyRegressionTests
{
    [Fact]
    public async Task SameProcessWaitersDoNotPollTheDistributedLeaseConcurrently()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var gate = new OrchestrationInstanceLocalGate();
        var first = Coordinator(repository, provider, gate);
        var second = Coordinator(repository, provider, gate);
        var instanceId = Id.New();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstMutation = first.TryExecuteAsync(instanceId, async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondMutation = second.TryExecuteAsync(instanceId, _ => Task.CompletedTask);

        await Task.Delay(100);
        await repository.Received(1).TryAcquireLease(instanceId, Arg.Any<string>(), Arg.Any<DateTime>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        release.TrySetResult();
        Assert.True(await firstMutation);
        Assert.True(await secondMutation);
        await repository.Received(2).TryAcquireLease(instanceId, Arg.Any<string>(), Arg.Any<DateTime>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivationConflictDoesNotPreventOtherPersistedChildrenFromActivating()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var coordinator = Coordinator(repository, provider);
        var instanceId = Id.New();
        var siblingActivated = false;
        await Assert.ThrowsAsync<RuntimeStateConflictException>(() => coordinator.TryExecuteAsync(instanceId, async token =>
        {
            var handoff = (IOrchestrationPostLeaseWork)coordinator;
            await handoff.ExecuteAfterLeaseReleaseAsync(instanceId,
                _ => Task.FromException(new RuntimeStateConflictException("A callback accepted concurrently.")), token);
            await handoff.ExecuteAfterLeaseReleaseAsync(instanceId,
                _ => { siblingActivated = true; return Task.CompletedTask; }, token);
        }));
        Assert.True(siblingActivated);
    }

    [Fact]
    public async Task PersistedDispatchActivationRunsAfterLeaseReleaseWithLiveCallerToken()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var coordinator = Coordinator(repository, provider);
        var instanceId = Id.New();
        var activated = false;
        Assert.True(await coordinator.TryExecuteAsync(instanceId, async token =>
        {
            await ((IOrchestrationPostLeaseWork)coordinator).ExecuteAfterLeaseReleaseAsync(instanceId, async activationToken =>
            {
                Assert.False(activationToken.IsCancellationRequested);
                await repository.Received(1).ReleaseLease(instanceId, Arg.Any<string>(), CancellationToken.None);
                activated = true;
            }, token);
            Assert.False(activated);
        }));
        Assert.True(activated);
    }

    [Fact]
    public async Task CommittedDispatchOverlapsDurabilityButInputWaitsForTheBarrier()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var storage = new PipelinedRuntimeStorageUnitOfWork();
        var coordinator = Coordinator(repository, provider, storageUnitOfWork: storage);
        var instanceId = Id.New();
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = coordinator.TryExecuteAsync(instanceId, async token =>
        {
            await ((IOrchestrationPostLeaseWork)coordinator).ExecuteIsolatedAfterLeaseReleaseAsync(
                instanceId,
                (_, _) => { activated.TrySetResult(); return Task.CompletedTask; },
                token);
        });

        await storage.Transaction.LogicallyCommitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(execution.IsCompleted);

        storage.Transaction.DurabilityReleased.TrySetResult();
        Assert.True(await execution.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(storage.Transaction.DurabilityAwaited);
    }

    [Fact]
    public async Task LocalOnlyCoordinationAlsoDispatchesAfterLogicalCommitBeforeDurability()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var storage = new PipelinedRuntimeStorageUnitOfWork();
        var coordinator = Coordinator(
            repository,
            provider,
            storageUnitOfWork: storage,
            distributedLeaseEnabled: false);
        var instanceId = Id.New();
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var execution = coordinator.TryExecuteAsync(instanceId, async token =>
        {
            await ((IOrchestrationPostLeaseWork)coordinator).ExecuteIsolatedAfterLeaseReleaseAsync(
                instanceId,
                (_, _) => { activated.TrySetResult(); return Task.CompletedTask; },
                token);
        });

        await storage.Transaction.LogicallyCommitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(execution.IsCompleted);
        await repository.DidNotReceive().TryAcquireLease(
            Arg.Any<Id>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());

        storage.Transaction.DurabilityReleased.TrySetResult();
        Assert.True(await execution.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(storage.Transaction.DurabilityAwaited);
    }

    [Fact]
    public async Task FailedMutationDoesNotActivateItsBufferedDispatches()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var coordinator = Coordinator(repository, provider);
        var instanceId = Id.New();
        var activated = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.TryExecuteAsync(instanceId, async token =>
        {
            await ((IOrchestrationPostLeaseWork)coordinator).ExecuteAfterLeaseReleaseAsync(instanceId, _ =>
            { activated = true; return Task.CompletedTask; }, token);
            throw new InvalidOperationException("mutation rolled back");
        }));
        Assert.False(activated);
        await repository.Received(1).ReleaseLease(instanceId, Arg.Any<string>(), CancellationToken.None);
    }

    [Fact]
    public async Task LostOrFailedRenewalCannotAcknowledgeADelegateThatIgnoresCancellation()
    {
        foreach (var renewalThrows in new[] { false, true })
        {
            var repository = CreateRepository();
            var renewalRepository = Substitute.For<IOrchestrationInstanceRepository>();
            renewalRepository.TryAcquireLease(Arg.Any<Id>(), Arg.Any<string>(), Arg.Any<DateTime>(),
                    Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .Returns(renewalThrows
                    ? Task.FromException<OrchestrationInstanceLease>(new InvalidOperationException("storage unavailable"))
                    : Task.FromResult<OrchestrationInstanceLease>(null!));
            await using var provider = new ServiceCollection()
                .AddScoped<IOrchestrationInstanceRepository>(_ => renewalRepository).BuildServiceProvider();
            var coordinator = Coordinator(repository, provider);
            var instanceId = Id.New();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.TryExecuteAsync(instanceId, async token =>
            {
                var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = token.Register(() => cancelled.TrySetResult());
                await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                // Deliberately return normally despite the coordinator cancelling this token.
            }));

            await repository.Received(1).ReleaseLease(instanceId, Arg.Any<string>(), CancellationToken.None);
        }
    }

    [Fact]
    public async Task CallerCancellationCannotBeAcknowledgedAsSuccess()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var instanceId = Id.New();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Coordinator(repository, provider)
            .TryExecuteAsync(instanceId, _ => { cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token));

        await repository.Received(1).ReleaseLease(instanceId, Arg.Any<string>(), CancellationToken.None);
    }

    [Fact]
    public async Task MutationFailureIsPreservedAndLeaseIsReleased()
    {
        var repository = CreateRepository();
        await using var provider = new ServiceCollection()
            .AddScoped<IOrchestrationInstanceRepository>(_ => repository).BuildServiceProvider();
        var failure = new InvalidOperationException("mutation failed");
        var instanceId = Id.New();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Coordinator(repository, provider)
            .TryExecuteAsync(instanceId, _ => Task.FromException(failure)));

        Assert.Same(failure, actual);
        await repository.Received(1).ReleaseLease(instanceId, Arg.Any<string>(), CancellationToken.None);
    }

    [Fact]
    public async Task FiftyNotificationsAndLocalDeliveryProgressWhileRemoteFanoutIsBlocked()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var fanout = new ControlledFanout(block: true);
        var allLocal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var localCount = 0;
        using var dispatcher = Dispatcher(queue, fanout, () =>
        {
            if (Interlocked.Increment(ref localCount) == 50) allLocal.TrySetResult();
        });
        var publisher = new SignalRRuntimeReactiveEventPublisher(queue, fanout);
        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            await publisher.Publish(CreateEvent());
            await fanout.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var publications = Enumerable.Range(1, 49).Select(_ => publisher.Publish(CreateEvent())).ToArray();
            Assert.All(publications, task => Assert.True(task.IsCompletedSuccessfully));
            await allLocal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(fanout.Published);
        }
        finally
        {
            fanout.Release.TrySetResult();
            await dispatcher.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task FanoutFailureRequestsReconciliationAndDoesNotStopTheNextEvent()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var fanout = new ControlledFanout(failFirst: true);
        var publisher = new SignalRRuntimeReactiveEventPublisher(queue, fanout);
        var first = CreateEvent();
        var second = CreateEvent();
        second.Revision = "before";
        second.Payload = JsonNode.Parse("{\"secret\":1}");
        await publisher.Publish(first);
        await publisher.Publish(second);
        second.Revision = "after";
        using var dispatcher = Dispatcher(queue, fanout);
        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            await fanout.Resynchronized.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var delivered = fanout.Published.Last();
            Assert.Equal(second.Id, delivered.Id);
            Assert.Equal("before", delivered.Revision);
            Assert.Null(delivered.Payload);
        }
        finally { await dispatcher.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task OverflowIsBoundedAndRequestsOneRemoteReconciliation()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var fanout = new ControlledFanout();
        var publisher = new SignalRRuntimeReactiveEventPublisher(queue, fanout);
        for (var index = 0; index < 5_000; index++)
            Assert.True(publisher.Publish(CreateEvent()).IsCompletedSuccessfully);
        Assert.True(queue.TryTakeResynchronizationRequired());
        using var dispatcher = Dispatcher(queue, fanout);
        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            await fanout.Resynchronized.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { await dispatcher.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.InRange(fanout.Published.Count, 1, 4096);
        Assert.Equal(1, fanout.ResyncCount);
    }

    [Fact]
    public async Task RemoteNotificationsAreNotRepublishedToOtherReplicas()
    {
        var queue = new SignalRRuntimeReactiveEventQueue();
        var fanout = new ControlledFanout();
        var remote = CreateEvent();
        var sentinel = CreateEvent();
        using var dispatcher = Dispatcher(queue, fanout);
        fanout.Deliver(remote);
        await new SignalRRuntimeReactiveEventPublisher(queue, fanout).Publish(sentinel);
        await dispatcher.StartAsync(CancellationToken.None);
        try { await fanout.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await dispatcher.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(sentinel.Id, Assert.Single(fanout.Published).Id);
    }

    private static DefaultOrchestrationInstanceCoordinator Coordinator(
        IOrchestrationInstanceRepository repository, ServiceProvider provider,
        OrchestrationInstanceLocalGate localGate = null,
        IRuntimeStorageUnitOfWork storageUnitOfWork = null,
        bool distributedLeaseEnabled = true)
        => new(repository, provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OrchestrationCoordinationOptions
            {
                LeaseDurationSeconds = 1,
                DistributedLeaseEnabled = distributedLeaseEnabled
            }),
            NullLogger<DefaultOrchestrationInstanceCoordinator>.Instance,
            storageUnitOfWork ?? new NoopRuntimeStorageUnitOfWork(),
            localGate: localGate);

    private sealed class PipelinedRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        public PipelinedTransaction Transaction { get; } = new();
        public bool AutoSaveChanges => false;
        public bool PreferBatchedEngineActivations => true;

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IRuntimeStorageTransaction>(Transaction);

        public IDisposable DeferAutoSave() => NoopDisposable.Instance;
        public Task SaveChanges(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PipelinedTransaction : IRuntimePipelinedStorageTransaction
    {
        public TaskCompletionSource LogicallyCommitted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DurabilityReleased { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DurabilityAwaited { get; private set; }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await CommitLogicalAsync(cancellationToken);
            await AwaitDurabilityAsync();
        }

        public Task CommitLogicalAsync(CancellationToken cancellationToken = default)
        {
            LogicallyCommitted.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task AwaitDurabilityAsync()
        {
            DurabilityAwaited = true;
            await DurabilityReleased.Task;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }

    private static IOrchestrationInstanceRepository CreateRepository()
    {
        var repository = Substitute.For<IOrchestrationInstanceRepository>();
        repository.TryAcquireLease(Arg.Any<Id>(), Arg.Any<string>(), Arg.Any<DateTime>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new OrchestrationInstanceLease(
                call.ArgAt<Id>(0), call.ArgAt<string>(1), call.ArgAt<DateTime>(2), call.ArgAt<DateTime>(3))));
        repository.ReleaseLease(Arg.Any<Id>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return repository;
    }

    private static SignalRRuntimeReactiveEventDispatcher Dispatcher(
        SignalRRuntimeReactiveEventQueue queue, ControlledFanout fanout, Action? onLocal = null)
    {
        var hub = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        clients.Group(Arg.Any<string>()).Returns(proxy);
        proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(call => { if (call.ArgAt<string>(0) == "runtime.transition") onLocal?.Invoke(); return Task.CompletedTask; });
        return new(queue, hub, NullLogger<SignalRRuntimeReactiveEventDispatcher>.Instance, fanout);
    }

    private static RuntimeReactiveEvent CreateEvent() => new()
    {
        Id = Id.New(), Revision = "1", EventName = "runtime.transition.created", TransitionType = "TaskCompleted",
        OrchestrationDefinitionKey = "concurrency", OrchestrationInstanceId = Id.New(), CorrelationId = "test",
        SagaId = "test", ExecutionKey = "test", InstanceStatus = "Running", ProducedBy = "tests"
    };

    private sealed class ControlledFanout(bool block = false, bool failFirst = false) : IRuntimeReactiveFanout
    {
        public event Action<RuntimeReactiveEvent>? Received;
        public event Action? ResyncRequired { add { } remove { } }
        public ConcurrentQueue<RuntimeReactiveEvent> Published { get; } = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resynchronized { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ResyncCount;
        public async Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        {
            Published.Enqueue(eventData);
            Entered.TrySetResult();
            if (block) await Release.Task.WaitAsync(cancellationToken);
            if (failFirst && Published.Count == 1) throw new InvalidOperationException("fanout unavailable");
        }
        public Task PublishResyncAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ResyncCount);
            Resynchronized.TrySetResult();
            return Task.CompletedTask;
        }
        public void Deliver(RuntimeReactiveEvent eventData) => Received?.Invoke(eventData);
    }
}
