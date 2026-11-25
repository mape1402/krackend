namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Diagnostics;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Mule;
using Xunit.Abstractions;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

// These tests kill a separate testhost process, not the process running the test suite.
// SQL Server, RabbitMQ and Redis are isolated Testcontainers owned by the parent fixture.
[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class RuntimeCrashRecoveryRealInfrastructureTests(
    RuntimeRealInfrastructureFixture infrastructure,
    ITestOutputHelper output)
{
    [Fact(Timeout = 180_000)]
    public async Task MissingFailureProjectionColumnsCloseRuntimeAdmission()
    {
        var database = infrastructure.CreateDatabaseName();
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database))
        {
            await runtime.QueryAsync(async db =>
            {
                await db.Database.ExecuteSqlRawAsync("DROP INDEX [IX_SagaFailureEvents_OrchestrationKey_OrchestrationVersion_TransitionType_OccurredOnUtc] ON [Runtime].[SagaFailureEvents]");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Runtime].[SagaFailureEvents] DROP COLUMN [OrchestrationKey]");
                return true;
            });
        }
        await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database);
        var snapshot = await restarted.Services.GetRequiredService<IRuntimeOperationalStateController>().EvaluateAsync();
        output.WriteLine($"Runtime state={snapshot.State}; reasons={string.Join("; ", snapshot.Dependencies.Where(x => !x.IsAvailable).Select(x => x.Reason))}");
        Assert.Equal(RuntimeOperationalState.Closed, snapshot.State);
        Assert.Contains(snapshot.Dependencies, dependency => !dependency.IsAvailable &&
            dependency.Reason is not null && dependency.Reason.Contains("OrchestrationKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Timeout = 240_000)]
    public async Task TwoReplicasRedeliverCallbackAfterInstanceLeaseContention()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"lease.trigger.{suffix}";
        var topic = $"lease.task.{suffix}";
        var database = infrastructure.CreateDatabaseName();
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure, [new(topic, version.ToString())]);
        downstream.Scenario.Enqueue(topic, RealMessagingServiceOutcome.NoReply());
        await using var first = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, replicaId: $"lease-a-{suffix}");
        var artifact = Artifact($"lease-{suffix}", version, $"lease-{suffix}", [EventTrigger(trigger, version)],
            Stage("first", 1, MessagingTask(topic, 1, version,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(90), "Timeout"))));
        var deployment = await first.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await first.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
        var waiting = await PollingAssert.EventuallyAsync(
            () => first.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == topic)),
            task => task?.Status == TaskExecutionStatus.WaitingResponse,
            "Task did not enter WaitingResponse.", TimeSpan.FromSeconds(40));
        var invocation = await PollingAssert.EventuallyAsync(
            () => Task.FromResult(downstream.Scenario.Invocations.FirstOrDefault(x => x.Topic == topic)),
            item => item is not null, "Downstream command did not arrive.", TimeSpan.FromSeconds(20));
        await using var second = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, replicaId: $"lease-b-{suffix}");
        await second.WaitForArtifactReadyAsync(deployment.RuntimeArtifactId);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var scope = second.Services.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>();
        var held = coordinator.TryExecuteAsync(waiting!.OrchestrationInstanceId, async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var metadata = invocation!.MessageMetadata;
            var result = new OrchestrationExecutionResultMetadata { Succeeded = true, Status = "Completed", CompletedOnUtc = DateTime.UtcNow };
            await first.PublishBackchannelAsync(metadata.ReplyAddress!, metadata, result, BusinessPayload(("response", true)));
            await Task.Delay(TimeSpan.FromSeconds(4));
            var during = await first.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(x => x.Id == waiting.Id));
            Assert.Equal(TaskExecutionStatus.WaitingResponse, during.Status);
        }
        finally
        {
            release.TrySetResult();
            await held;
        }
        var completed = await first.WaitForInstanceStatusAsync(suffix, OrchestrationInstanceStatus.Completed, TimeSpan.FromSeconds(75));
        var transitions = await first.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
            .CountAsync(x => x.OrchestrationInstanceId == completed.Id && x.TransitionType == "TaskCallbackCompleted"));
        Assert.Equal(1, transitions);
        Assert.Equal(1, downstream.Scenario.CountInvocations(topic));
    }

    [Fact(Timeout = 180_000)]
    public async Task InvalidOverdueTasksDoNotBlockLaterValidTimeouts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"starvation.trigger.{suffix}";
        var topic = $"starvation.task.{suffix}";
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure, [new(topic, version.ToString())]);
        downstream.Scenario.Enqueue(topic, RealMessagingServiceOutcome.NoReply());
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact($"starvation-{suffix}", version, $"starvation-{suffix}", [EventTrigger(trigger, version)],
            Stage("first", 1, MessagingTask(topic, 1, version,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(180), "Timeout"))));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
        var valid = await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == topic)),
            item => item?.Status == TaskExecutionStatus.WaitingResponse && item.ResponseDeadlineUtc.HasValue,
            "Task was not waiting with a timer.", TimeSpan.FromSeconds(40));
        var validAttempt = await runtime.QueryAsync(db => db.TaskExecutionAttempts.AsNoTracking()
            .SingleAsync(item => item.TaskExecutionId == valid!.Id && item.AttemptNumber == valid.LastAttemptNumber));
        string? validTimerId;
        await using (var timerScope = runtime.Services.CreateAsyncScope())
        {
            validTimerId = await timerScope.ServiceProvider.GetRequiredService<IOrchestrationDurableWorkInspector>()
                .FindTimeoutActionIdAsync(valid!, validAttempt);
        }
        Assert.False(string.IsNullOrWhiteSpace(validTimerId));
        foreach (var service in runtime.Services.GetServices<IHostedService>().Where(x =>
            x.GetType().Name is "RuntimeReconciliationBackgroundService" or "OrchestrationTimeoutBackgroundService"))
            await service.StopAsync(CancellationToken.None);

        await runtime.QueryAsync(async db =>
        {
            var row = await db.TaskExecutions.SingleAsync(x => x.Id == valid!.Id);
            row.ResponseDeadlineUtc = DateTime.UtcNow.AddMinutes(-1);
            row.TimeoutActionId = validTimerId;
            row.TimeoutActionLane = MuleActionKeys.TimeoutLane;
            for (var index = 0; index < 100; index++)
                db.TaskExecutions.Add(new TaskExecution
                {
                    Id = Id.New(), OrchestrationInstanceId = row.OrchestrationInstanceId,
                    StageExecutionId = row.StageExecutionId, TaskKey = $"invalid-{index}",
                    Status = TaskExecutionStatus.WaitingResponse, TaskKind = row.TaskKind,
                    ExecutionMode = row.ExecutionMode, AwaitResponse = true,
                    ResponseDeadlineUtc = DateTime.UtcNow.AddMinutes(-2),
                    WaitingSinceUtc = DateTime.UtcNow.AddMinutes(-5),
                    TimeoutActionId = row.TimeoutActionId, TimeoutActionLane = row.TimeoutActionLane
                });
            await db.SaveChangesAsync();
            return true;
        });
        await using var scope = runtime.Services.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<ITaskResponseTimeoutScheduler>();
        for (var pass = 0; pass < 3; pass++) await scheduler.RepairAsync(100);
        var afterScans = await runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(x => x.Id == valid!.Id));
        output.WriteLine($"After three recovery scans: valid task={afterScans.Status}.");
        if (afterScans.Status == TaskExecutionStatus.WaitingResponse)
        {
            var direct = await scope.ServiceProvider.GetRequiredService<IOrchestrationTimeoutProcessor>()
                .ProcessTaskTimeoutAsync(valid!.Id, DateTime.UtcNow);
            output.WriteLine($"Processing that same timeout directly: applied={direct.Applied}.");
            Assert.True(direct.Applied, "The control timeout must be independently processable.");
        }
        Assert.NotEqual(TaskExecutionStatus.WaitingResponse, afterScans.Status);
    }

    [Theory(Timeout = 300_000)]
    [InlineData("before-callback")]
    [InlineData("after-callback")]
    public async Task CallbackWorkSurvivesAbruptProcessDeath(string crashPoint)
        => await RunCallbackCrashAsync(crashPoint, failedReply: false, timeoutSeconds: 180, timeoutCheck: false);

    [Theory(Timeout = 300_000)]
    [InlineData("before-callback")]
    [InlineData("after-callback")]
    public async Task FailedCallbackSurvivesAbruptProcessDeath(string crashPoint)
        => await RunCallbackCrashAsync(crashPoint, failedReply: true, timeoutSeconds: 180, timeoutCheck: false);

    [Fact(Timeout = 300_000)]
    public async Task PendingCallbackPreventsTimeoutWhileRuntimeRecovers()
        => await RunCallbackCrashAsync("before-callback", failedReply: false, timeoutSeconds: 60, timeoutCheck: true);

    [Fact(Timeout = 600_000)]
    public async Task AcceptedSuccessDoesNotTimeoutWhileCallbackIsRecovering()
        => await RunCallbackCrashAsync("before-callback", failedReply: false, timeoutSeconds: 60, timeoutCheck: false, defaultLockTest: true);

    [Fact(Timeout = 600_000)]
    public async Task CallbackEventuallyRecoversAfterDefaultMuleLockExpires()
        => await RunCallbackCrashAsync("before-callback", failedReply: false, timeoutSeconds: 600, timeoutCheck: false, defaultLockTest: true);

    [Theory(Timeout = 300_000)]
    [InlineData("dispatch-before-send")]
    [InlineData("dispatch-after-send")]
    public async Task DispatchSurvivesAbruptProcessDeath(string crashPoint)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"crash.dispatch.trigger.{suffix}";
        var topic = $"crash.dispatch.task.{suffix}";
        var database = infrastructure.CreateDatabaseName();
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure, [new(topic, version.ToString())]);
        if (crashPoint == "dispatch-after-send") downstream.Scenario.Enqueue(topic, RealMessagingServiceOutcome.NoReply());
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15)))
        {
            var artifact = Artifact($"crash-dispatch-{suffix}", version, $"crash-dispatch-{suffix}", [EventTrigger(trigger, version)],
                Stage("first", 1, MessagingTask(topic, 1, version,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(120), "Timeout"))));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        }
        var marker = Path.Combine(Path.GetTempPath(), $"krackend-crash-{suffix}.marker");
        try
        {
            using var child = StartCrashProcess(database, new OrchestrationMessageMetadata { CorrelationId = suffix }, marker,
                crashPoint, failedReply: false, TimeSpan.FromSeconds(15), trigger, version.ToString());
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try { await child.WaitForExitAsync(limit.Token); }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new TimeoutException("Dispatch crash test child did not terminate.");
            }
            var childOutput = await stdout;
            var childError = await stderr;
            Assert.True(File.Exists(marker), $"Dispatch crash point was not reached.\n{childOutput}\n{childError}");
            Assert.NotEqual(0, child.ExitCode);
            await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15));
            var until = DateTime.UtcNow.AddSeconds(75);
            OrchestrationInstance? instance = null;
            do
            {
                instance = await restarted.QueryAsync(db => db.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == suffix));
                if (instance?.Status is OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.DeadLettered) break;
                await Task.Delay(1000);
            } while (DateTime.UtcNow < until);
            var tasks = await restarted.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(x => x.OrchestrationInstanceId == instance!.Id).ToArrayAsync());
            output.WriteLine($"Dispatch crash={crashPoint}; instance={instance?.Status}; tasks={string.Join(", ", tasks.Select(x => $"{x.TaskKey}:{x.Status}"))}; downstream invocations={downstream.Scenario.CountInvocations(topic)}. Downstream has no idempotency in this test.");
            Assert.Equal(OrchestrationInstanceStatus.Completed, instance?.Status);
            Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == topic).Status);
            Assert.True(downstream.Scenario.CountInvocations(topic) >= 1);
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact(Timeout = 300_000)]
    public async Task TimeoutContinuationSurvivesAbruptProcessDeath()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"crash.timeout.trigger.{suffix}";
        var taskTopic = $"crash.timeout.task.{suffix}";
        var database = infrastructure.CreateDatabaseName();
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure, [new(taskTopic, version.ToString())]);
        downstream.Scenario.Enqueue(taskTopic, RealMessagingServiceOutcome.NoReply());
        OrchestrationMessageMetadata metadata;
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15)))
        {
            var artifact = Artifact($"crash-timeout-{suffix}", version, $"crash-timeout-{suffix}", [EventTrigger(trigger, version)],
                Stage("first", 1, MessagingTask(taskTopic, 1, version,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(30), "Timeout"))));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
            await PollingAssert.EventuallyAsync(
                () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == taskTopic)),
                task => task?.Status == TaskExecutionStatus.WaitingResponse && task.ResponseDeadlineUtc.HasValue,
                "Task must be waiting before the restart.", TimeSpan.FromSeconds(40));
            var invocation = await PollingAssert.EventuallyAsync(
                () => Task.FromResult(downstream.Scenario.Invocations.FirstOrDefault(x => x.Topic == taskTopic)),
                item => item is not null, "Downstream command did not arrive.", TimeSpan.FromSeconds(20));
            metadata = invocation!.MessageMetadata;
        }
        var marker = Path.Combine(Path.GetTempPath(), $"krackend-crash-{suffix}.marker");
        try
        {
            using var child = StartCrashProcess(database, metadata, marker, "timeout-after-persist", failedReply: false, TimeSpan.FromSeconds(15));
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(110));
            try { await child.WaitForExitAsync(limit.Token); }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new TimeoutException("Timeout crash test child did not terminate.");
            }
            var childOutput = await stdout;
            var childError = await stderr;
            Assert.True(File.Exists(marker), $"The timeout continuation crash point was not reached.\n{childOutput}\n{childError}");
            Assert.NotEqual(0, child.ExitCode);
            await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15));
            var until = DateTime.UtcNow.AddSeconds(45);
            OrchestrationInstance? instance = null;
            do
            {
                instance = await restarted.QueryAsync(db => db.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == suffix));
                if (instance?.Status is OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.DeadLettered) break;
                await Task.Delay(1000);
            } while (DateTime.UtcNow < until);
            var task = await restarted.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(x => x.TaskKey == taskTopic));
            output.WriteLine($"After restart: instance={instance?.Status}; task={task.Status}; invocations={downstream.Scenario.CountInvocations(taskTopic)}.");
            var recoveredAutomatically = instance?.Status is OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.DeadLettered;
            if (!recoveredAutomatically)
            {
                await using var scope = restarted.Services.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>().ReconcileAsync(instance!.Id.ToString());
                output.WriteLine($"Manual reconciliation returned {result.Status}: {string.Join("; ", result.Findings.Select(f => f.Message))}");
            }
            Assert.Equal(TaskExecutionStatus.TimedOut, task.Status);
            Assert.True(recoveredAutomatically, "The saga must continue automatically after its timeout was persisted.");
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact(Timeout = 300_000)]
    public async Task ParallelCallbacksSurviveAbruptProcessDeath()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"crash.parallel.trigger.{suffix}";
        var first = $"crash.parallel.first.{suffix}";
        var second = $"crash.parallel.second.{suffix}";
        var after = $"crash.parallel.after.{suffix}";
        var group = Id.New();
        var database = infrastructure.CreateDatabaseName();
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure,
            [new(first, version.ToString()), new(second, version.ToString()), new(after, version.ToString())]);
        downstream.Scenario.Enqueue(first, RealMessagingServiceOutcome.NoReply());
        downstream.Scenario.Enqueue(second, RealMessagingServiceOutcome.NoReply());
        OrchestrationMessageMetadata firstMetadata;
        OrchestrationMessageMetadata secondMetadata;
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15)))
        {
            var artifact = Artifact($"crash-parallel-{suffix}", version, $"crash-parallel-{suffix}", [EventTrigger(trigger, version)],
                StageWithGraph("parallel", 1, [new ParallelGroupArtifact(group, ParallelJoinPolicy.WaitAll, null)], [],
                    MessagingTask(first, 1, version, timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(180), "Timeout"),
                        executionMode: TaskExecutionMode.Parallel, parallelGroupId: group),
                    MessagingTask(second, 2, version, timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(180), "Timeout"),
                        executionMode: TaskExecutionMode.Parallel, parallelGroupId: group)),
                Stage("after", 2, MessagingTask(after, 1, version)));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
            await PollingAssert.EventuallyAsync(
                () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(x => x.TaskKey == first || x.TaskKey == second).ToArrayAsync()),
                tasks => tasks.Length == 2 && tasks.All(x => x.Status == TaskExecutionStatus.WaitingResponse),
                "Both tasks must wait for their respective replies.", TimeSpan.FromSeconds(40));
            await PollingAssert.EventuallyAsync(() => Task.FromResult(downstream.Scenario.Invocations.Count),
                count => count >= 2, "Both downstream commands must arrive.", TimeSpan.FromSeconds(20));
            firstMetadata = downstream.Scenario.Invocations.Single(x => x.Topic == first).MessageMetadata;
            secondMetadata = downstream.Scenario.Invocations.Single(x => x.Topic == second).MessageMetadata;
        }
        var marker = Path.Combine(Path.GetTempPath(), $"krackend-crash-{suffix}.marker");
        try
        {
            using var child = StartCrashProcess(database, firstMetadata, marker, "before-callback", failedReply: false, TimeSpan.FromSeconds(15));
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try { await child.WaitForExitAsync(limit.Token); }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new TimeoutException("Parallel crash test child did not terminate.");
            }
            var childOutput = await stdout;
            var childError = await stderr;
            Assert.True(File.Exists(marker), $"Parallel callback crash point was not reached.\n{childOutput}\n{childError}");
            Assert.NotEqual(0, child.ExitCode);
            await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15));
            await restarted.PublishBackchannelAsync(secondMetadata.ReplyAddress!, secondMetadata,
                new OrchestrationExecutionResultMetadata { Succeeded = true, Status = "Completed", CompletedOnUtc = DateTime.UtcNow },
                BusinessPayload(("secondResponse", true)));
            var until = DateTime.UtcNow.AddSeconds(75);
            OrchestrationInstance? instance = null;
            do
            {
                instance = await restarted.QueryAsync(db => db.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == suffix));
                if (instance?.Status == OrchestrationInstanceStatus.Completed) break;
                await Task.Delay(1000);
            } while (DateTime.UtcNow < until);
            var tasks = await restarted.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(x => x.OrchestrationInstanceId == instance!.Id).ToArrayAsync());
            output.WriteLine($"Parallel instance={instance?.Status}; tasks={string.Join(", ", tasks.Select(x => $"{x.TaskKey}:{x.Status}"))}; after invocations={downstream.Scenario.CountInvocations(after)}.");
            Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == second).Status);
            Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == first).Status);
            Assert.Equal(OrchestrationInstanceStatus.Completed, instance?.Status);
            Assert.Equal(1, downstream.Scenario.CountInvocations(after));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact(Timeout = 300_000)]
    public async Task ParallelGroupContinuesWhenLastCallbackWasPersistedBeforeCrash()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"crash.join.trigger.{suffix}";
        var first = $"crash.join.first.{suffix}";
        var second = $"crash.join.second.{suffix}";
        var after = $"crash.join.after.{suffix}";
        var group = Id.New();
        var database = infrastructure.CreateDatabaseName();
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure,
            [new(first, version.ToString()), new(second, version.ToString()), new(after, version.ToString())]);
        downstream.Scenario.Enqueue(second, RealMessagingServiceOutcome.NoReply());
        OrchestrationMessageMetadata lastMetadata;
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15)))
        {
            var artifact = Artifact($"crash-join-{suffix}", version, $"crash-join-{suffix}", [EventTrigger(trigger, version)],
                StageWithGraph("parallel", 1, [new ParallelGroupArtifact(group, ParallelJoinPolicy.WaitAll, null)], [],
                    MessagingTask(first, 1, version, timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(180), "Timeout"),
                        executionMode: TaskExecutionMode.Parallel, parallelGroupId: group),
                    MessagingTask(second, 2, version, timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(180), "Timeout"),
                        executionMode: TaskExecutionMode.Parallel, parallelGroupId: group)),
                Stage("after", 2, MessagingTask(after, 1, version)));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
            await PollingAssert.EventuallyAsync(
                () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(x => x.TaskKey == first || x.TaskKey == second).ToArrayAsync()),
                tasks => tasks.Length == 2 && tasks.Any(x => x.TaskKey == first && x.Status == TaskExecutionStatus.Completed) &&
                    tasks.Any(x => x.TaskKey == second && x.Status == TaskExecutionStatus.WaitingResponse),
                "One parallel task must complete while the other awaits its response.", TimeSpan.FromSeconds(45));
            var invocation = await PollingAssert.EventuallyAsync(
                () => Task.FromResult(downstream.Scenario.Invocations.FirstOrDefault(x => x.Topic == second)),
                item => item is not null, "Second parallel command did not arrive.", TimeSpan.FromSeconds(20));
            lastMetadata = invocation!.MessageMetadata;
        }
        var marker = Path.Combine(Path.GetTempPath(), $"krackend-crash-{suffix}.marker");
        try
        {
            using var child = StartCrashProcess(database, lastMetadata, marker, "after-callback", failedReply: false, TimeSpan.FromSeconds(15));
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try { await child.WaitForExitAsync(limit.Token); }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new TimeoutException("Parallel join crash test child did not terminate.");
            }
            var childOutput = await stdout;
            var childError = await stderr;
            Assert.True(File.Exists(marker), $"Parallel join crash point was not reached.\n{childOutput}\n{childError}");
            Assert.NotEqual(0, child.ExitCode);
            await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: TimeSpan.FromSeconds(15));
            var until = DateTime.UtcNow.AddSeconds(60);
            OrchestrationInstance? instance = null;
            do
            {
                instance = await restarted.QueryAsync(db => db.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == suffix));
                if (instance?.Status == OrchestrationInstanceStatus.Completed) break;
                await Task.Delay(1000);
            } while (DateTime.UtcNow < until);
            var tasks = await restarted.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(x => x.OrchestrationInstanceId == instance!.Id).ToArrayAsync());
            output.WriteLine($"Join instance={instance?.Status}; tasks={string.Join(", ", tasks.Select(x => $"{x.TaskKey}:{x.Status}"))}; after invocations={downstream.Scenario.CountInvocations(after)}.");
            if (instance?.Status == OrchestrationInstanceStatus.Running)
            {
                await using var scope = restarted.Services.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>().ReconcileAsync(instance.Id.ToString());
                output.WriteLine($"Manual reconciliation={result.Status}: {string.Join("; ", result.Findings.Select(x => x.Message))}");
            }
            Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == first).Status);
            Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == second).Status);
            Assert.Equal(OrchestrationInstanceStatus.Completed, instance?.Status);
            Assert.Equal(1, downstream.Scenario.CountInvocations(after));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    private async Task RunCallbackCrashAsync(string crashPoint, bool failedReply, int timeoutSeconds, bool timeoutCheck, bool defaultLockTest = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var trigger = $"crash.trigger.{suffix}";
        var first = $"crash.first.{suffix}";
        var second = $"crash.second.{suffix}";
        var database = infrastructure.CreateDatabaseName();
        var lockTimeout = timeoutCheck || defaultLockTest ? (TimeSpan?)null : TimeSpan.FromSeconds(15);
        await using var downstream = await RealMessagingServiceHost.StartAsync(infrastructure,
            [new(first, version.ToString()), new(second, version.ToString())]);
        downstream.Scenario.Enqueue(first, RealMessagingServiceOutcome.NoReply());
        downstream.Scenario.Enqueue(second, RealMessagingServiceOutcome.Success(BusinessPayload(("second", true))));

        OrchestrationMessageMetadata metadata;
        DateTime? responseDeadline;
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: lockTimeout))
        {
            var artifact = Artifact($"crash-{suffix}", version, $"crash-{suffix}", [EventTrigger(trigger, version)],
                Stage("first", 1, MessagingTask(first, 1, version,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(timeoutSeconds), "Timeout"))),
                Stage("second", 2, MessagingTask(second, 1, version)));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("start", true)), suffix);
            var waiting = await PollingAssert.EventuallyAsync(
                () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == first)),
                task => task?.Status == TaskExecutionStatus.WaitingResponse && task.ResponseDeadlineUtc.HasValue,
                "The first task did not enter WaitingResponse.", TimeSpan.FromSeconds(40));
            responseDeadline = waiting!.ResponseDeadlineUtc;
            var invocation = await PollingAssert.EventuallyAsync(
                () => Task.FromResult(downstream.Scenario.Invocations.FirstOrDefault(x => x.Topic == first)),
                item => item is not null, "The downstream did not receive the first command.", TimeSpan.FromSeconds(20));
            metadata = invocation!.MessageMetadata;
            Assert.NotNull(metadata.ReplyAddress);
        }

        var marker = Path.Combine(Path.GetTempPath(), $"krackend-crash-{suffix}.marker");
        try
        {
            using var child = StartCrashProcess(database, metadata, marker, crashPoint, failedReply, lockTimeout);
            var childOutput = child.StandardOutput.ReadToEndAsync();
            var childError = child.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try
            {
                await child.WaitForExitAsync(deadline.Token);
            }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
                throw new TimeoutException($"Crash test child did not terminate at {crashPoint}.");
            }
            var capturedOutput = await childOutput;
            var capturedError = await childError;
            Assert.True(File.Exists(marker), $"Child did not reach {crashPoint}; exit={child.ExitCode}.\n{capturedOutput}\n{capturedError}");
            Assert.NotEqual(0, child.ExitCode);
            var crashedAt = DateTime.Parse(await File.ReadAllTextAsync(marker), null, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.True(responseDeadline.HasValue && crashedAt < responseDeadline.Value,
                $"Crash must precede the task deadline: crash={crashedAt:o}, deadline={responseDeadline:o}.");
            output.WriteLine($"Process terminated at {crashPoint}; exit code {child.ExitCode}.");

            await using var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database, muleLockTimeout: lockTimeout);
            OrchestrationInstance? observed = null;
            var until = DateTime.UtcNow.AddSeconds(defaultLockTest ? 390 : timeoutCheck ? 95 : 75);
            do
            {
                observed = await restarted.QueryAsync(db => db.OrchestrationInstances.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CorrelationId == suffix));
                if (observed?.Status is OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.DeadLettered or OrchestrationInstanceStatus.Idle) break;
                await Task.Delay(1000);
            } while (DateTime.UtcNow < until);

            var tasks = await restarted.QueryAsync(db => db.TaskExecutions.AsNoTracking()
                .Where(x => x.OrchestrationInstanceId == observed!.Id).ToArrayAsync());
            output.WriteLine($"Instance={observed?.Status}; tasks={string.Join(", ", tasks.Select(x => $"{x.TaskKey}:{x.Status}"))}; first invocations={downstream.Scenario.CountInvocations(first)}; second invocations={downstream.Scenario.CountInvocations(second)}");
            var firstTask = tasks.Single(x => x.TaskKey == first);
            var firstAttempt = await restarted.QueryAsync(db => db.TaskExecutionAttempts.AsNoTracking()
                .SingleAsync(x => x.TaskExecutionId == firstTask.Id && x.AttemptNumber == firstTask.LastAttemptNumber));
            output.WriteLine($"Attempt={firstAttempt.Status}; original response saved={firstAttempt.ResponsePayload?["originalResponse"]}; error code={firstAttempt.ErrorCode}.");
            if (firstTask.Status == TaskExecutionStatus.Completed)
                Assert.True(firstAttempt.ResponsePayload?["originalResponse"]?.GetValue<bool>() == true, "The original response payload must be retained.");
            if (firstTask.Status == TaskExecutionStatus.Failed)
                Assert.Equal("CrashTestFailure", firstAttempt.ErrorCode);
            var actions = await restarted.QueryAsync(db => db.Set<DurableAction>().AsNoTracking()
                .Where(x => x.Lane == "runtime-backchannel" || x.Lane == "runtime-timeout").ToArrayAsync());
            foreach (var action in actions)
                output.WriteLine($"Action key={action.Key}; lane={action.Lane}; status={action.Status}; attempts={action.Attempts}; locked={action.LockedOnUtc}; due={action.NextAttemptOnUtc}; error={action.LastError}");
            if (!timeoutCheck && observed?.Status == OrchestrationInstanceStatus.Running)
            {
                await using var scope = restarted.Services.CreateAsyncScope();
                var reconciliation = await scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>()
                    .ReconcileAsync(observed.Id.ToString());
                output.WriteLine($"Manual reconciliation={reconciliation.Status}: {string.Join("; ", reconciliation.Findings.Select(x => x.Message))}");
            }
            if (timeoutCheck)
            {
                Assert.DoesNotContain(tasks, task => task.Status == TaskExecutionStatus.TimedOut);
                if (observed?.Status == OrchestrationInstanceStatus.Completed)
                {
                    Assert.All(tasks, task => Assert.Equal(TaskExecutionStatus.Completed, task.Status));
                    Assert.Equal(1, downstream.Scenario.CountInvocations(first));
                    Assert.Equal(1, downstream.Scenario.CountInvocations(second));
                }
                else
                {
                    Assert.Equal(OrchestrationInstanceStatus.Running, observed?.Status);
                    Assert.Equal(TaskExecutionStatus.WaitingResponse, tasks.Single(x => x.TaskKey == first).Status);
                }
                return;
            }
            Assert.Equal(failedReply ? OrchestrationInstanceStatus.DeadLettered : OrchestrationInstanceStatus.Completed, observed?.Status);
            Assert.Equal(failedReply ? TaskExecutionStatus.Failed : TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == first).Status);
            if (!failedReply) Assert.Equal(TaskExecutionStatus.Completed, tasks.Single(x => x.TaskKey == second).Status);
            Assert.Equal(1, downstream.Scenario.CountInvocations(first));
            Assert.Equal(failedReply ? 0 : 1, downstream.Scenario.CountInvocations(second));
        }
        finally
        {
            if (File.Exists(marker)) File.Delete(marker);
        }
    }

    [Fact(Timeout = 120_000)]
    public async Task CrashChildProcess()
    {
        if (Environment.GetEnvironmentVariable("KRACKEND_DISASTER_CHILD") != "1") return;
        var database = Environment.GetEnvironmentVariable("KRACKEND_DISASTER_DATABASE")!;
        var marker = Environment.GetEnvironmentVariable("KRACKEND_DISASTER_MARKER")!;
        var point = Environment.GetEnvironmentVariable("KRACKEND_DISASTER_POINT")!;
        var failedReply = Environment.GetEnvironmentVariable("KRACKEND_DISASTER_FAILURE") == "1";
        var lockSeconds = Environment.GetEnvironmentVariable("KRACKEND_DISASTER_LOCK_SECONDS");
        TimeSpan? lockTimeout = int.TryParse(lockSeconds, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
        var metadata = JsonSerializer.Deserialize<OrchestrationMessageMetadata>(
            Convert.FromBase64String(Environment.GetEnvironmentVariable("KRACKEND_DISASTER_METADATA")!))!;
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, database,
            configureTestServices: services =>
            {
                if (point.StartsWith("dispatch-", StringComparison.Ordinal))
                {
                    services.AddScoped<MessagingRemoteCommandExecutor>();
                    services.RemoveAllKeyed<IRemoteCommandExecutor>(RemoteCommandTransport.Messaging);
                    services.AddKeyedScoped<IRemoteCommandExecutor>(RemoteCommandTransport.Messaging, (sp, _) =>
                        new CrashDispatchExecutor(sp.GetRequiredService<MessagingRemoteCommandExecutor>(), marker, point));
                    return;
                }
                if (point == "timeout-after-persist")
                {
                    services.AddScoped<SagaEngine>();
                    services.Replace(ServiceDescriptor.Scoped<ISagaEngine>(sp =>
                        new CrashTimeoutEngine(sp.GetRequiredService<SagaEngine>(), marker)));
                    return;
                }
                services.AddScoped<CompleteCallbackDecisionHandler>();
                services.Replace(ServiceDescriptor.Scoped<IDecisionHandler<CompleteCallbackDecision>>(sp =>
                    new CrashCallbackHandler(sp.GetRequiredService<CompleteCallbackDecisionHandler>(), marker, point)));
            }, muleLockTimeout: lockTimeout);
        if (point.StartsWith("dispatch-", StringComparison.Ordinal))
        {
            await runtime.PublishTriggerAsync(Environment.GetEnvironmentVariable("KRACKEND_DISASTER_TRIGGER")!,
                Environment.GetEnvironmentVariable("KRACKEND_DISASTER_VERSION")!, BusinessPayload(("start", true)), metadata.CorrelationId);
            await Task.Delay(TimeSpan.FromSeconds(90));
            Assert.Fail("The dispatch crash point was not reached.");
        }
        if (point == "timeout-after-persist")
        {
            await Task.Delay(TimeSpan.FromSeconds(90));
            Assert.Fail("The timeout crash point was not reached.");
        }
        await runtime.PublishBackchannelAsync(metadata.ReplyAddress!, metadata,
            new OrchestrationExecutionResultMetadata
            {
                Succeeded = !failedReply, CompletedOnUtc = DateTime.UtcNow,
                Status = failedReply ? "Failed" : "Completed", ErrorCode = failedReply ? "CrashTestFailure" : null!,
                ErrorMessage = failedReply ? "Downstream failure for crash test" : null!
            },
            BusinessPayload(("originalResponse", true)));
        await Task.Delay(TimeSpan.FromSeconds(90));
        Assert.Fail("The callback crash point was not reached.");
    }

    private Process StartCrashProcess(string database, OrchestrationMessageMetadata metadata, string marker, string point, bool failedReply, TimeSpan? lockTimeout = null, string? trigger = null, string? version = null)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add("vstest");
        info.ArgumentList.Add(typeof(RuntimeCrashRecoveryRealInfrastructureTests).Assembly.Location);
        info.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~RuntimeCrashRecoveryRealInfrastructureTests.CrashChildProcess");
        info.Environment["KRACKEND_DISASTER_CHILD"] = "1";
        info.Environment["KRACKEND_DISASTER_RABBIT"] = infrastructure.RabbitMqConnectionString;
        info.Environment["KRACKEND_DISASTER_REDIS"] = infrastructure.RedisConnectionString;
        info.Environment["KRACKEND_DISASTER_SQL"] = infrastructure.MasterSqlConnectionString;
        info.Environment["KRACKEND_DISASTER_DATABASE"] = database;
        info.Environment["KRACKEND_DISASTER_METADATA"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(metadata));
        info.Environment["KRACKEND_DISASTER_MARKER"] = marker;
        info.Environment["KRACKEND_DISASTER_POINT"] = point;
        info.Environment["KRACKEND_DISASTER_FAILURE"] = failedReply ? "1" : "0";
        if (lockTimeout.HasValue) info.Environment["KRACKEND_DISASTER_LOCK_SECONDS"] = ((int)lockTimeout.Value.TotalSeconds).ToString();
        if (trigger is not null) info.Environment["KRACKEND_DISASTER_TRIGGER"] = trigger;
        if (version is not null) info.Environment["KRACKEND_DISASTER_VERSION"] = version;
        info.Environment["COMPlus_DbgEnableMiniDump"] = "0";
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start crash test process.");
    }

    private sealed class CrashCallbackHandler(CompleteCallbackDecisionHandler inner, string marker, string point)
        : IDecisionHandler<CompleteCallbackDecision>
    {
        public async Task HandleAsync(CompleteCallbackDecision decision, CancellationToken cancellationToken = default)
        {
            if (point == "before-callback") Crash();
            await inner.HandleAsync(decision, cancellationToken);
            if (point == "after-callback") Crash();
        }

        private void Crash()
        {
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            Environment.FailFast($"Intentional test process termination at {point}.");
        }
    }

    private sealed class CrashTimeoutEngine(SagaEngine inner, string marker) : ISagaEngine
    {
        public Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default)
            => inner.StartOrchestrationAsync(intent, cancellationToken);

        public Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default)
        {
            if (intent.ExecutionResultMetadata?.ErrorType == "Timeout")
            {
                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
                Environment.FailFast("Intentional test process termination after persisting timeout.");
            }
            return inner.OrchestrateAsync(intent, cancellationToken);
        }
    }

    private sealed class CrashDispatchExecutor(MessagingRemoteCommandExecutor inner, string marker, string point) : IRemoteCommandExecutor
    {
        public async Task ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            if (point == "dispatch-before-send") Crash();
            await inner.ExecuteAsync(command, cancellationToken);
            if (point == "dispatch-after-send") Crash();
        }

        private void Crash()
        {
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            Environment.FailFast($"Intentional test process termination at {point}.");
        }
    }
}
