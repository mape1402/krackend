using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mule;
using Mule.Dispatching;
using Mule.Diagnostics;

// Two independent instance IDs. A's competing messages consume both real Mule lane executors.
var probe = new Probe();
var pipelineResults = new List<object>();
await using var provider = new ServiceCollection().AddSingleton(probe)
    .AddScoped<IOrchestrationInstanceRepository, TestRepository>()
    .AddScoped<IOrchestrationInstanceCoordinator>(sp => new DefaultOrchestrationInstanceCoordinator(
        sp.GetRequiredService<IOrchestrationInstanceRepository>(), sp.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new OrchestrationCoordinationOptions()), NullLogger<DefaultOrchestrationInstanceCoordinator>.Instance))
    .AddScoped<TestStorage>()
    .AddScoped<IMuleStorage>(sp => sp.GetRequiredService<TestStorage>())
    .AddSingleton<IMuleActionHandler, Handler>()
    .AddSingleton<MuleActionRegistry>().BuildServiceProvider();
var settings = new MuleSettings { CleanupMode = MuleCleanupMode.Disabled };
settings.Lanes["runtime-backchannel"] = new MuleLaneSettings { MaxDegreeOfParallelism = 2, ExecutionQueueCapacity = 4 };
var executor = new MuleActionExecutor(provider.GetRequiredService<IServiceScopeFactory>(), new MuleSchedulerSignal(),
    Options.Create(settings), new MuleRuntimeMetrics(), NullLogger<MuleActionExecutor>.Instance);
using var stop = new CancellationTokenSource();
var workers = executor.Start(stop.Token);
await using var ownerScope = provider.CreateAsyncScope();
var releaseOwner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var ownerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var owner = ownerScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(probe.BusyInstance, async token => {
    ownerEntered.SetResult(); await releaseOwner.Task.WaitAsync(token);
});
await ownerEntered.Task;
try
{
    await executor.EnqueueRangeAsync([Message(probe.BusyInstance), Message(probe.BusyInstance)], stop.Token);
    await probe.BothBusyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    // Control: B's lease is immediately available while A is held.
    await using var controlScope = provider.CreateAsyncScope();
    var independentLeaseAvailable = await controlScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>()
        .TryExecuteAsync(probe.HealthyInstance, _ => Task.CompletedTask, waitForLease: false);
    if (!independentLeaseAvailable) throw new Exception("The independent instance control failed.");
    probe.QueuedAt.Restart();
    await executor.EnqueueAsync(Message(probe.HealthyInstance), stop.Token);
    await Task.Delay(150);
    var healthyStartedBeforeContendersFinished = probe.HealthyStarted.Task.IsCompleted;
    await probe.HealthyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    var deadline = Stopwatch.StartNew();
    while (probe.Failures.Count < 2 && deadline.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(10);
    if (healthyStartedBeforeContendersFinished || probe.HealthyQueueDelayMs < 1000 || probe.Failures.Count != 2 || probe.ScopeIds.Count != 3)
        throw new Exception("Cross-instance lane blocking did not reproduce.");
    pipelineResults.Add(new {
        test = "busy_instance_messages_block_independent_instance_in_mule_executor",
        runtimeProcesses = 1, processId = Environment.ProcessId, serviceProviders = 1,
        localContentionReportedAsAnotherRuntimeNode = probe.FailureMessages.Count == 2 &&
            probe.FailureMessages.All(message => message.Contains("another runtime node", StringComparison.Ordinal)),
        distinctInstances = 2, laneExecutionSlots = 2, independentLeaseAvailable,
        healthyStartedBeforeContendersFinished, healthyQueueDelayMs = probe.HealthyQueueDelayMs,
        busyActionsFailed = probe.Failures.Count,
        busyActionsRetryDelaySeconds = probe.Failures.ToArray(),
        distinctActionScopeServices = probe.ScopeIds.Count,
        explanation = "Two A contenders occupy both execution slots while waiting for A's lease; B has a free lease but waits in the shared lane."
    });
}
finally
{
    releaseOwner.TrySetResult();
    await owner;
    await stop.CancelAsync();
    await Task.WhenAll(workers);
}
// Actual commit notifier: a full bounded queue can keep the caller's saga lease occupied.
{
    var queueSettings = new MuleSettings();
    queueSettings.Lanes["runtime-dispatch"] = new MuleLaneSettings { DispatchQueueCapacity = 1 };
    var queue = new ChannelMuleDispatchQueue(Options.Create(queueSettings));
    var notifier = new AmbientTransactionMuleCommitNotifier(queue, new MuleSchedulerSignal(), Options.Create(queueSettings));
    await queue.EnqueueAsync(new Mule.Ulid(Guid.NewGuid()), "runtime-dispatch");
    var heldInstance = Id.New();
    await using var publisherScope = provider.CreateAsyncScope();
    await using var waiterScope = provider.CreateAsyncScope();
    var notificationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var publishing = publisherScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(heldInstance, async token => {
        notificationEntered.SetResult();
        await notifier.NotifySavedAsync(new Mule.Ulid(Guid.NewGuid()), "runtime-dispatch", token);
    });
    await notificationEntered.Task;
    bool sameInstanceAvailable;
    bool completedBeforeQueueDrained;
    try
    {
        await Task.Delay(150);
        completedBeforeQueueDrained = publishing.IsCompleted;
        sameInstanceAvailable = await waiterScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>()
            .TryExecuteAsync(heldInstance, _ => Task.CompletedTask, waitForLease: false);
    }
    finally
    {
        await queue.DequeueAsync("runtime-dispatch");
        await publishing.WaitAsync(TimeSpan.FromSeconds(3));
    }
    if (completedBeforeQueueDrained || sameInstanceAvailable) throw new Exception("Bounded notification queue did not hold the caller's lease.");
    pipelineResults.Add(new {
        test = "bounded_commit_notification_queue_holds_callers_instance_lease",
        queueCapacity = 1, completedBeforeQueueDrained, sameInstanceLeaseAvailableBeforeDrain = sameInstanceAvailable,
        completedAfterQueueDrain = publishing.IsCompletedSuccessfully,
        explanation = "NotifySavedAsync awaits a bounded queue write inside the caller's lease; the lease stays occupied until capacity returns."
    });
}
Console.WriteLine(JsonSerializer.Serialize(pipelineResults, new JsonSerializerOptions { WriteIndented = true }));
DurableAction Message(Id id) => new() { Key = "Backchannel", Lane = "runtime-backchannel", Payload = id.Value.ToString() };

sealed class Probe
{
    public readonly Id BusyInstance = Id.New(), HealthyInstance = Id.New();
    public readonly object Gate = new();
    public readonly Dictionary<Id, (string Owner, DateTime Expires)> Leases = new();
    public readonly TaskCompletionSource BothBusyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource HealthyStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly Stopwatch QueuedAt = new();
    public readonly ConcurrentBag<double> Failures = new();
    public readonly ConcurrentBag<string> FailureMessages = new();
    public readonly ConcurrentDictionary<Guid, byte> ScopeIds = new();
    public int BusyStarted;
    public long HealthyQueueDelayMs;
}
sealed class Handler(Probe probe) : IMuleActionHandler
{
    public async Task ExecuteAsync(DurableAction action, IServiceProvider services, CancellationToken token)
    {
        probe.ScopeIds.TryAdd(services.GetRequiredService<TestStorage>().ScopeId, 0);
        var id = new Id(Guid.Parse(action.Payload));
        if (id == probe.BusyInstance && Interlocked.Increment(ref probe.BusyStarted) == 2) probe.BothBusyStarted.SetResult();
        var executed = await services.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(id, _ => {
            if (id == probe.HealthyInstance) { probe.HealthyQueueDelayMs = probe.QueuedAt.ElapsedMilliseconds; probe.HealthyStarted.SetResult(); }
            return Task.CompletedTask;
        }, token);
        if (!executed) throw new OrchestrationInstanceLeaseUnavailableException(id);
    }
}
sealed class TestStorage(Probe probe) : IMuleStorage
{
    public readonly Guid ScopeId = Guid.NewGuid();
    public Task MarkCompletedAsync(Guid id, DateTimeOffset completed, CancellationToken token) => Task.CompletedTask;
    public Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? next, CancellationToken token)
    {
        probe.FailureMessages.Add(error);
        if (next is { } retry) probe.Failures.Add((retry - now).TotalSeconds);
        return Task.CompletedTask;
    }
    public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
}
sealed class TestRepository(Probe probe) : IOrchestrationInstanceRepository
{
    public Task<OrchestrationInstanceLease> TryAcquireLease(Id id, string leaseId, DateTime now, DateTime expires, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (probe.Gate)
        {
            if (probe.Leases.TryGetValue(id, out var lease) && lease.Owner != leaseId && lease.Expires > now)
                return Task.FromResult<OrchestrationInstanceLease>(null);
            probe.Leases[id] = (leaseId, expires);
            return Task.FromResult(new OrchestrationInstanceLease(id, leaseId, now, expires));
        }
    }
    public Task ReleaseLease(Id id, string leaseId, CancellationToken cancellationToken = default)
    { lock (probe.Gate) if (probe.Leases.TryGetValue(id, out var lease) && lease.Owner == leaseId) probe.Leases.Remove(id); return Task.CompletedTask; }
}
