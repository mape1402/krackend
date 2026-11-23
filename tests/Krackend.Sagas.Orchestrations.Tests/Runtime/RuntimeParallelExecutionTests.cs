using System.Diagnostics;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mule;
using Id = Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeParallelExecutionTests
{
    [Fact]
    public async Task BurstsCompleteRealSagasWithTwoExecutorsAndEarlyCallbacks()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        var report = new List<object>();
        var completed = 0;
        foreach (var count in new[] { 1, 10, 20, 50, 100 })
        {
            var clock = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(0, count).Select(index => runtime.AcceptAsync($"burst-{count}-{index}")));
            var acceptedMs = clock.Elapsed.TotalMilliseconds;
            completed += count;
            await runtime.WaitForCompletedAsync(completed, TimeSpan.FromSeconds(30));
            var totalMs = clock.Elapsed.TotalMilliseconds;
            var instances = await runtime.ReadAsync(db => db.OrchestrationInstances.AsNoTracking()
                .Where(x => x.CorrelationId.StartsWith($"burst-{count}-")).ToArrayAsync());
            var latency = instances.Select(x => (x.LastUpdatedOnUtc - x.StartedOnUtc).TotalMilliseconds).Order().ToArray();
            var inputs = await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking()
                .Where(x => x.Lane == "runtime-trigger" && x.CorrelationId.StartsWith($"burst-{count}-"))
                .ToArrayAsync());
            Assert.Equal(count, instances.Length);
            Assert.Empty(inputs);
            Assert.All(instances, instance => Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status));
            Assert.True(latency.Last() < 10000, $"A saga exceeded 10 seconds: {latency.Last()} ms.");
            report.Add(new { count, acceptedMs, totalMs,
                sagaP50Ms = latency[latency.Length / 2], sagaP95Ms = latency[(int)((latency.Length - 1) * .95)], sagaMaxMs = latency.Last(),
                triggerRecoveryActions = inputs.Length });
        }
        var failed = await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Status == DurableActionStatus.Failed));
        Assert.Equal(0, failed);
        Assert.Equal(completed, runtime.Publications.Commands.Count);
        await File.WriteAllTextAsync("artifacts/concurrency-burst-results.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task BusySagaCallbacksReleaseTheirExecutorsSoAnotherSagaCompletesFirst()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync(parallel: true);
        runtime.Publications.AutoCallback = false;
        await runtime.AcceptAsync("A");
        await runtime.AcceptAsync("B");
        await WaitAsync(() => Task.FromResult(runtime.Publications.Commands.Count >= 4));
        await WaitAsync(async () => await runtime.ReadAsync(db => db.TaskDispatches.CountAsync(x => x.DispatchStatus == "WaitingResponse")) == 4);
        var a = runtime.Publications.Commands.Where(x => x.MessageMetadata.CorrelationId == "A").ToArray();
        var b = runtime.Publications.Commands.Where(x => x.MessageMetadata.CorrelationId == "B").ToArray();
        Assert.Equal(2, a.Length);
        var aInstanceId = Parse(a[0].OrchestrationInstanceId);
        await using var owner = runtime.Provider.CreateAsyncScope();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = owner.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(
            aInstanceId, async token => { entered.SetResult(); await release.Task.WaitAsync(token); });
        await entered.Task;
        try
        {
            var accepted = Stopwatch.StartNew();
            foreach (var command in a) await runtime.CallbackAsync(command);
            Assert.True(accepted.Elapsed < TimeSpan.FromSeconds(1), "Callback acceptance waited on the held saga lease.");
            await WaitAsync(async () => await runtime.ReadAsync(db =>
                (from dispatch in db.TaskDispatches
                 join attempt in db.TaskExecutionAttempts on dispatch.TaskExecutionAttemptId equals attempt.Id
                 join task in db.TaskExecutions on attempt.TaskExecutionId equals task.Id
                 where dispatch.CallbackPending && task.OrchestrationInstanceId == aInstanceId
                 select dispatch).CountAsync()) == 2);
            foreach (var command in b) await runtime.CallbackAsync(command);
            await runtime.WaitForCompletedAsync(1, TimeSpan.FromSeconds(5));
            Assert.False(release.Task.IsCompleted);
            var completed = await runtime.ReadAsync(db => db.OrchestrationInstances.AsNoTracking().SingleAsync(x => x.Status == OrchestrationInstanceStatus.Completed));
            Assert.Equal("B", completed.CorrelationId);
            var pending = await runtime.ReadAsync(db =>
                (from dispatch in db.TaskDispatches.AsNoTracking()
                 join attempt in db.TaskExecutionAttempts.AsNoTracking() on dispatch.TaskExecutionAttemptId equals attempt.Id
                 join task in db.TaskExecutions.AsNoTracking() on attempt.TaskExecutionId equals task.Id
                 where dispatch.CallbackPending && task.OrchestrationInstanceId == aInstanceId
                 select dispatch).ToArrayAsync());
            Assert.Equal(2, pending.Length);
            Assert.All(pending, dispatch => Assert.False(string.IsNullOrWhiteSpace(dispatch.CallbackReceiptId)));
        }
        finally { release.TrySetResult(); await held; }
        await runtime.WaitForCompletedAsync(2);
    }

    [Fact]
    public async Task ParallelPublicationsOfOneSagaBothStartBeforeEitherFinishes()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync(parallel: true);
        runtime.Publications.Hold = true;
        await runtime.AcceptAsync("parallel");
        try
        {
            await runtime.Publications.BothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, runtime.Publications.Commands.Count);
            var command = runtime.Publications.Commands.First();
            await using var scope = runtime.Provider.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(
                Parse(command.OrchestrationInstanceId), _ => Task.CompletedTask, waitForLease: false));
        }
        finally { runtime.Publications.Release.TrySetResult(); }
        await runtime.WaitForCompletedAsync(1);
    }

    [Fact]
    public async Task DirectActivationCompletesOneHundredInputsWithoutMuleExecutors()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync(start: false);
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(index => runtime.AcceptAsync($"offline-{index}")));
        var acceptedMs = clock.Elapsed.TotalMilliseconds;
        // This is a single-file SQLite harness with one writer, not the PostgreSQL
        // load environment. Its purpose is proving progress without Mule executors;
        // the production latency budget is enforced by the external load test.
        Assert.True(acceptedMs < 15000, $"Direct activation took {acceptedMs} ms in the SQLite harness.");
        await runtime.WaitForCompletedAsync(100, TimeSpan.FromSeconds(30));
        Assert.Equal(0, await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Lane == "runtime-trigger" || x.Lane == "runtime-backchannel" || x.Lane == "runtime-dispatch")));
        await File.WriteAllTextAsync("artifacts/concurrency-activation-results.json", JsonSerializer.Serialize(new { inputs = 100, acceptedMs, muleExecutorsStarted = false, completed = 100 }));
    }

    [Fact]
    public async Task CallbackReceiptWinsAgainstStaleTimeoutAndSurvivesInterruptedHandoff()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        runtime.Publications.AutoCallback = false;
        await runtime.AcceptAsync("receipt");
        await WaitAsync(async () => await runtime.ReadAsync(db => db.TaskDispatches.CountAsync(x => x.DispatchStatus == "WaitingResponse")) == 1);
        var command = runtime.Publications.Commands.Single();
        await using var staleScope = runtime.Provider.CreateAsyncScope();
        var staleRepo = staleScope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>();
        var stale = await staleRepo.GetById(Parse(command.DispatchId));
        await using var intakeScope = runtime.Provider.CreateAsyncScope();
        var receipts = Assert.IsAssignableFrom<ITaskDispatchCallbackReceiptRepository>(intakeScope.ServiceProvider.GetRequiredService<ITaskDispatchRepository>());
        Assert.Null(await receipts.TryAcceptCallback(stale.Id, Id.New().ToString(), "{}", DateTime.UtcNow,
            identity: new TaskCallbackIdentity(Parse(command.OrchestrationInstanceId), Parse(command.TaskExecutionId),
                command.MessageMetadata.Attempt + 1, runtime.ArtifactId)));
        var work = runtime.Callback(command);
        await intakeScope.ServiceProvider.GetRequiredService<IIntakeBuffer>().EnqueueWorkAsync(new WorkItem
        {
            ArtifactId = Id.New().ToString(), IngressKind = work.IngressKind, IngressTransport = work.IngressTransport,
            MessageMetadata = work.MessageMetadata, ExecutionResultMetadata = work.ExecutionResultMetadata, Payload = work.Payload
        });
        Assert.Null((await staleRepo.GetById(stale.Id)).CallbackReceiptId);
        Assert.Equal(0, await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Lane == "runtime-backchannel")));
        work.CallbackReceiptId = Id.New().ToString();
        var accepted = await receipts.TryAcceptCallback(stale.Id, work.CallbackReceiptId, JsonSerializer.Serialize(work), DateTime.UtcNow);
        Assert.True(accepted.CallbackPending);
        stale.DispatchStatus = "TimedOut";
        await Assert.ThrowsAsync<RuntimeStateConflictException>(() => staleRepo.Update(stale));
        Assert.Equal("WaitingResponse", (await staleRepo.GetById(stale.Id)).DispatchStatus);
        var recovered = await intakeScope.ServiceProvider.GetRequiredService<IOrchestrationCallbackRecovery>().RecoverAsync(10);
        Assert.Equal(1, recovered);
        await runtime.WaitForCompletedAsync(1);
        await WaitAsync(async () => !(await staleRepo.GetById(stale.Id)).CallbackPending);
        await runtime.CallbackAsync(command);
        Assert.Equal(1, await runtime.ReadAsync(db => db.ExecutionTransitions.CountAsync(x => x.TransitionType == "TaskCallbackCompleted")));
    }

    [Fact]
    public async Task ExpiredOwnerCannotCommitAfterLeaseTakeover()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        runtime.Publications.AutoCallback = false;
        await runtime.AcceptAsync("fence");
        await WaitAsync(async () => await runtime.ReadAsync(db => db.TaskDispatches.CountAsync(x => x.DispatchStatus == "WaitingResponse")) == 1);
        var instanceId = Parse(runtime.Publications.Commands.Single().OrchestrationInstanceId);
        await using var oldScope = runtime.Provider.CreateAsyncScope();
        var oldRepo = oldScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var lease = await oldRepo.TryAcquireLease(instanceId, "old", DateTime.UtcNow, DateTime.UtcNow.AddSeconds(10));
        Assert.NotNull(lease);
        var old = await oldRepo.GetById(instanceId);
        await using var nextScope = runtime.Provider.CreateAsyncScope();
        var nextRepo = nextScope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        Assert.NotNull(await nextRepo.TryAcquireLease(instanceId, "new", DateTime.UtcNow.AddSeconds(20), DateTime.UtcNow.AddMinutes(1)));
        using (oldScope.ServiceProvider.GetRequiredService<RuntimeMutationLeaseScope>().Enter(lease))
        {
            old.Status = OrchestrationInstanceStatus.Completed;
            await Assert.ThrowsAsync<RuntimeStateConflictException>(() => oldRepo.Update(old));
        }
        var current = await nextRepo.GetById(instanceId);
        Assert.NotEqual(OrchestrationInstanceStatus.Completed, current.Status);
        Assert.Equal("new", current.ActiveLeaseId);
    }

    [Fact]
    public async Task SelectiveRoutesAndActiveStageReadsAvoidUnrelatedRowsAndObserveRetirement()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync(start: false);
        await using var scope = runtime.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IRuntimeIngressConfigurationRepository>();
        var configurations = Enumerable.Range(0, 1000).Select(index => new RuntimeIngressConfiguration
        {
            Id = Id.New(), RuntimeOrchestrationArtifactId = runtime.ArtifactId, ConfigurationKey = $"trigger:{index}",
            IngressKind = IngressKind.Trigger, IngressTransport = IngressTransport.Messaging,
            SettingsPayload = JsonSerializer.Serialize(new { topic = $"events.{index}", version = "1.0.0" }),
            IsActive = true, CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow
        }).ToArray();
        await repository.UpsertForArtifactAsync(runtime.ArtifactId, configurations);
        var routes = Assert.IsAssignableFrom<ISelectiveMessagingIngressAccessor>(services.GetRequiredService<IGetAllIngressConfigurationsAccessor>());
        Assert.Single(await routes.ReadMatchingAsync("EVENTS.19", "1.0.0"));
        var lifecycle = Assert.IsAssignableFrom<IRuntimeSelectiveTriggerIngressRepository>(repository);
        await lifecycle.DeactivateTriggersAsync(runtime.ArtifactId, ["trigger:19"]);
        Assert.Empty(await routes.ReadMatchingAsync("events.19", "1.0.0"));
        await Assert.IsAssignableFrom<IRuntimeReactivatableTriggerIngressRepository>(repository)
            .ActivateTriggersAsync(runtime.ArtifactId, ["trigger:19"]);
        Assert.Single(await routes.ReadMatchingAsync("events.19", "1.0.0"));

        var stageId = Id.New();
        var instanceId = Id.New();
        var tasks = Enumerable.Range(0, 100).Select(index => new TaskExecution
        { Id = Id.New(), OrchestrationInstanceId = instanceId, StageExecutionId = index < 2 ? stageId : Id.New(), TaskKey = $"task-{index}" }).ToArray();
        var db = services.GetRequiredService<RuntimeDbContext>();
        db.TaskExecutions.AddRange(tasks);
        await db.SaveChangesAsync();
        var reader = Assert.IsAssignableFrom<ITaskExecutionStageReader>(services.GetRequiredService<ITaskExecutionRepository>());
        Assert.Equal(2, (await reader.GetByStageExecutionId(stageId)).Count);
    }

    [Fact]
    public async Task CooperativeTimeoutKeepsItsActionAndAttemptBudget()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        runtime.Publications.AutoCallback = false;
        await runtime.AcceptAsync("timeout");
        // The timeout action is addressed by its deterministic Mule key. The task's
        // persisted deadline is the functional reference, so the healthy path no
        // longer needs a second SQL update just to copy the generated action id.
        await WaitAsync(async () =>
            await runtime.ReadAsync(db => db.TaskExecutions.CountAsync(x =>
                x.Status == TaskExecutionStatus.WaitingResponse && x.ResponseDeadlineUtc != null)) == 1 &&
            await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Lane == "runtime-timeout")) == 1);
        var command = runtime.Publications.Commands.Single();
        await using var scope = runtime.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? actionId = null;
        var owner = services.GetRequiredService<IOrchestrationInstanceCoordinator>().TryExecuteAsync(Parse(command.OrchestrationInstanceId), async token =>
        {
            var tasks = services.GetRequiredService<ITaskExecutionRepository>();
            var task = await tasks.GetById(Parse(command.TaskExecutionId), token);
            task.ResponseDeadlineUtc = DateTime.UtcNow.AddMilliseconds(150);
            await tasks.Update(task, token);
            var result = await services.GetRequiredService<ITaskResponseTimeoutScheduler>().ScheduleAsync(new TaskResponseTimeoutScheduleRequest
            {
                OrchestrationInstanceId = task.OrchestrationInstanceId, TaskExecutionId = task.Id,
                TaskExecutionAttemptId = Parse(command.TaskExecutionAttemptId), DispatchId = Parse(command.DispatchId),
                AttemptNumber = command.MessageMetadata.Attempt, DueOnUtc = task.ResponseDeadlineUtc.Value
            }, token);
            actionId = result.ActionId;
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitAsync(async () => (await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking().ToArrayAsync()))
                .Any(action => action.Id.ToString() == actionId && action.PendingReason == DurableActionPendingReason.Deferred));
            Assert.Equal(TaskExecutionStatus.WaitingResponse, await runtime.ReadAsync(db => db.TaskExecutions.Select(x => x.Status).SingleAsync()));
        }
        finally { release.TrySetResult(); Assert.True(await owner); }
        await WaitAsync(async () => await runtime.ReadAsync(db => db.TaskExecutions.CountAsync(x => x.Status == TaskExecutionStatus.TimedOut)) == 1);
        await WaitAsync(async () => (await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking().ToArrayAsync()))
            .Any(action => action.Id.ToString() == actionId && action.Status == DurableActionStatus.Completed));
        var timer = (await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking().ToArrayAsync())).Single(x => x.Id.ToString() == actionId);
        Assert.Equal(0, timer.Attempts);
        Assert.Null(timer.LastError);
    }

    [Fact]
    public async Task FutureRemoteCommandRunsThroughDurableRecoveryWithoutLocalWakeup()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync(start: false);
        runtime.Publications.AutoCallback = false;
        var due = DateTimeOffset.UtcNow.AddSeconds(1);
        await using (var scope = runtime.Provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IRemoteCommandDispatcher>().DispatchAsync(new RemoteCommand
            {
                RemoteCommandTransport = RemoteCommandTransport.Messaging, DispatchId = Id.New().ToString(),
                ScheduledOnUtc = due, Payload = "{}", SettingsPayload = "{}"
            });
        var original = await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking().SingleAsync());
        Assert.Equal(due, original.NextAttemptOnUtc);
        Assert.Empty(runtime.Publications.Commands);
        await runtime.StartAsync();
        await WaitAsync(() => Task.FromResult(runtime.Publications.Commands.Count == 1));
        Assert.True(DateTimeOffset.UtcNow >= due);
        await WaitAsync(async () => await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Status == DurableActionStatus.Completed)) == 1);
        var completed = await runtime.ReadAsync(db => db.Set<DurableAction>().AsNoTracking().SingleAsync());
        Assert.Equal(original.Id, completed.Id);
        Assert.Equal(original.CreatedOnUtc, completed.CreatedOnUtc);
        Assert.Equal(0, completed.Attempts);
    }

    [Fact]
    public async Task DistinctDurableStartsWithSharedBusinessCorrelationRemainIndependent()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        for (var index = 0; index < 2; index++)
        {
            await using var scope = runtime.Provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMuleClient>().EnqueueAsync(ActionKey.From("TriggerSaga"), new WorkItem
            {
                ArtifactId = runtime.ArtifactId.ToString(),
                IngressKind = Krackend.Sagas.Orchestrations.Runtime.Ingress.IngressKind.Trigger,
                IngressTransport = Krackend.Sagas.Orchestrations.Runtime.Ingress.IngressTransport.Messaging,
                MessageMetadata = new Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.OrchestrationMessageMetadata { CorrelationId = "shared" },
                Payload = System.Text.Json.Nodes.JsonNode.Parse("{}")!
            }, options =>
            {
                options.Lane = "runtime-trigger";
                options.CorrelationId = "shared";
                options.DeduplicationKey = $"distinct-start:{index}";
            });
        }
        await runtime.WaitForCompletedAsync(2);
        Assert.Equal(2, runtime.Publications.Commands.Select(x => x.OrchestrationInstanceId).Distinct().Count());
    }

    internal static Id Parse(string value) => new(System.Ulid.Parse(value));
    internal static async Task WaitAsync(Func<Task<bool>> condition)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(10)) { if (await condition()) return; await Task.Delay(20); }
        throw new TimeoutException("Concurrency condition did not complete within ten seconds.");
    }
}

public sealed class RuntimeSustainedConcurrencyTests
{
    [Fact]
    public async Task SixMessagesPerMinuteForFiveMinutesCompleteWithoutAccumulatingWaits()
    {
        await using var runtime = await ConcurrentMuleRuntimeHarness.CreateAsync();
        var clock = Stopwatch.StartNew();
        var latencies = new List<double>();
        for (var index = 0; index < 30; index++)
        {
            var due = TimeSpan.FromSeconds(index * 10) - clock.Elapsed;
            if (due > TimeSpan.Zero) await Task.Delay(due);
            var messageClock = Stopwatch.StartNew();
            await runtime.AcceptAsync($"sustained-{index}");
            await runtime.WaitForCompletedAsync(index + 1, TimeSpan.FromSeconds(5));
            latencies.Add(messageClock.Elapsed.TotalMilliseconds);
        }
        var remaining = TimeSpan.FromMinutes(5) - clock.Elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        Assert.Equal(30, await runtime.ReadAsync(db => db.OrchestrationInstances.CountAsync(x => x.Status == OrchestrationInstanceStatus.Completed)));
        Assert.Equal(0, await runtime.ReadAsync(db => db.Set<DurableAction>().CountAsync(x => x.Status == DurableActionStatus.Failed)));
        await File.WriteAllTextAsync("artifacts/concurrency-sustained-measurements.json", JsonSerializer.Serialize(new
        { inputs = 30, observationSeconds = clock.Elapsed.TotalSeconds, perMinute = 6, maxLatencyMs = latencies.Max(), latenciesMs = latencies }));
    }
}
