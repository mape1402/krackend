using System.Diagnostics;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var results = new List<object>();
ServiceProvider Provider(LeaseStore store, int duration = 30) => new ServiceCollection().AddSingleton(store)
    .AddScoped<IOrchestrationInstanceRepository, TestRepository>()
    .AddScoped<IOrchestrationInstanceCoordinator>(sp => new DefaultOrchestrationInstanceCoordinator(
        sp.GetRequiredService<IOrchestrationInstanceRepository>(), sp.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new OrchestrationCoordinationOptions { LeaseDurationSeconds = duration }),
        NullLogger<DefaultOrchestrationInstanceCoordinator>.Instance)).BuildServiceProvider();

// Control: independent instances are not blocked by a global coordinator lock.
{
    var store = new LeaseStore();
    await using var provider = Provider(store);
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ => {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>()
            .TryExecuteAsync(Id.New(), token => Task.Delay(30, token));
    }));
    if (outcomes.Count(x => x) != 20) throw new Exception("Control failed");
    results.Add(new { test = "independent_instances_control", completed = outcomes.Count(x => x), acquisitionCalls = store.Attempts });
}
// The default 2s/25ms lease loop under twenty concurrent contenders.
{
    var store = new LeaseStore();
    await using var provider = Provider(store);
    await using var ownerScope = provider.CreateAsyncScope();
    var id = Id.New();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var owner = ownerScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(id, async token => {
        entered.SetResult(); await release.Task.WaitAsync(token);
    });
    await entered.Task;
    var timer = Stopwatch.StartNew();
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ => {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(id, _ => Task.CompletedTask);
    }));
    release.SetResult(); await owner;
    if (outcomes.Any(x => x)) throw new Exception("A contender bypassed the lease");
    results.Add(new { test = "twenty_contenders_one_busy_instance", rejected = outcomes.Count(x => !x), acquisitionCalls = store.Attempts, elapsedMs = timer.ElapsedMilliseconds });
}
// Actual publisher and coordinator: slow optional fanout must not retain the lease.
{
    var store = new LeaseStore();
    await using var provider = Provider(store);
    await using var ownerScope = provider.CreateAsyncScope();
    await using var contenderScope = provider.CreateAsyncScope();
    var id = Id.New();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var publisher = new SignalRRuntimeReactiveEventPublisher(new SignalRRuntimeReactiveEventQueue(), new SlowFanout());
    var notification = new RuntimeReactiveEvent { Revision = "1", EventName = "audit", TransitionType = "audit", OrchestrationDefinitionKey = "audit", CorrelationId = "audit", SagaId = "audit", ExecutionKey = "audit", InstanceStatus = "Running", ProducedBy = "audit" };
    var timer = Stopwatch.StartNew();
    var owner = ownerScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(id, async _ => {
        entered.SetResult();
        for (var i = 0; i < 3; i++) await publisher.Publish(notification, CancellationToken.None);
    });
    await entered.Task;
    var accepted = await contenderScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(id, _ => Task.CompletedTask);
    await owner;
    if (!accepted) throw new Exception("Optional fanout still retained the saga lease");
    results.Add(new { test = "slow_dashboard_fanout_does_not_hold_lease", contenderAccepted = accepted, elapsedMs = timer.ElapsedMilliseconds });
}
// A delegate ignoring cancellation must not acknowledge lost ownership as success.
{
    var store = new LeaseStore { RejectRenewals = true };
    await using var provider = Provider(store, 1);
    await using var scope = provider.CreateAsyncScope();
    var observedCancellation = false;
    var success = false;
    var leaseLossReported = false;
    try
    {
        success = await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(Id.New(), async token => {
            await Task.Delay(850); observedCancellation = token.IsCancellationRequested;
        });
    }
    catch (OperationCanceledException) { leaseLossReported = true; }
    if (success || !observedCancellation || !leaseLossReported) throw new Exception("Lost lease was acknowledged as success");
    results.Add(new { test = "lost_lease_is_not_reported_as_success", returnedSuccess = success, observedCancellation, leaseLossReported });
}
// One missing SQL record repeatedly prevents an unrelated healthy terminal action from committing.
{
    var probe = new Mule.FastLane.Redis.DurableStoreProbe { ExistingId = Guid.NewGuid() };
    var buffer = new Mule.FastLane.Redis.RedisFastLaneBuffer { Terminals = [
        new Mule.DurableAction { Id = probe.ExistingId, Status = Mule.DurableActionStatus.Completed },
        new Mule.DurableAction { Id = Guid.NewGuid(), Status = Mule.DurableActionStatus.Completed }
    ] };
    await using var provider = new ServiceCollection().AddScoped<Mule.IMuleDurableStorage>(_ => new Mule.FastLane.Redis.TestDurableStorage(probe)).BuildServiceProvider();
    var service = new Mule.FastLane.Redis.FastLaneRedisFlushService(provider.GetRequiredService<IServiceScopeFactory>(), buffer,
        Options.Create(new Mule.FastLane.Redis.FastLaneRedisOptions()), NullLogger<Mule.FastLane.Redis.FastLaneRedisFlushService>.Instance);
    var method = service.GetType().GetMethod("FlushOnceAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var failures = 0;
    for (var i = 0; i < 3; i++)
    {
        try { await (Task)method.Invoke(service, [CancellationToken.None])!; }
        catch (InvalidOperationException exception) when (exception.Message.Contains("was not found")) { failures++; }
    }
    if (failures != 3 || probe.Committed != 0 || buffer.Terminals.Count != 2) throw new Exception("Poison-batch reproduction did not trigger");
    results.Add(new { test = "mule_poison_terminal_blocks_healthy_completion", failures, requeuedBatches = buffer.Requeues, committedHealthyCompletions = probe.Committed, remainingInBatch = buffer.Terminals.Count });
}
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

sealed class LeaseStore
{
    public readonly object Gate = new();
    public readonly Dictionary<Id, (string Owner, DateTime Expires)> Leases = new();
    public int Attempts;
    public bool RejectRenewals;
}
sealed class TestRepository(LeaseStore store) : IOrchestrationInstanceRepository
{
    public Task<OrchestrationInstanceLease> TryAcquireLease(Id id, string leaseId, DateTime now, DateTime expires, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (store.Gate)
        {
            store.Attempts++;
            if (store.Leases.TryGetValue(id, out var current))
            {
                if (store.RejectRenewals || (current.Owner != leaseId && current.Expires > now))
                    return Task.FromResult<OrchestrationInstanceLease>(null);
            }
            store.Leases[id] = (leaseId, expires);
            return Task.FromResult(new OrchestrationInstanceLease(id, leaseId, now, expires));
        }
    }
    public Task ReleaseLease(Id id, string leaseId, CancellationToken cancellationToken = default)
    {
        lock (store.Gate) if (store.Leases.TryGetValue(id, out var current) && current.Owner == leaseId) store.Leases.Remove(id);
        return Task.CompletedTask;
    }
}
sealed class SlowFanout : IRuntimeReactiveFanout
{
    public event Action<RuntimeReactiveEvent> Received { add { } remove { } }
    public event Action ResyncRequired { add { } remove { } }
    public Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default) => Task.Delay(1100, cancellationToken);
}
