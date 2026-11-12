namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class OrchestrationReconciliationRealInfrastructureTests(RuntimeRealInfrastructureFixture infrastructure)
{
    [Fact(Timeout = 240_000)]
    public async Task ReconciliationRepairsTwoParallelOverdueWaitsWithMissingTimestamps()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topics = new[] { $"reconcile.parallel.{suffix}.one", $"reconcile.parallel.{suffix}.two" };
        var trigger = $"reconcile.parallel.trigger.{suffix}";
        var group = Id.New();
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        foreach (var topic in topics)
            await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact($"reconcile-parallel-{suffix}", version, $"reconcile-parallel-{suffix}", [EventTrigger(trigger, version)],
            StageWithGraph("parallel", 1, [new ParallelGroupArtifact(group, ParallelJoinPolicy.WaitAll, null)], [],
                topics.Select((topic, index) => MessagingTask(topic, index + 1, version,
                    retryPolicy: index == 0 ? RetryPolicy(1, "UnconsumedTimeout") : null,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(60), "UnconsumedTimeout"),
                    executionMode: TaskExecutionMode.Parallel, parallelGroupId: group)).ToArray()));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("repro", true)), suffix);
        var waiting = await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(t => topics.Contains(t.TaskKey)).ToArrayAsync()),
            tasks => tasks.Length == 2 && tasks.All(task => task.Status == TaskExecutionStatus.WaitingResponse && !string.IsNullOrWhiteSpace(task.TimeoutActionId)),
            "Both parallel tasks must be dispatched and have timers.", TimeSpan.FromSeconds(35));

        // Isolate the manual reconciliation from background repair and the original timers.
        var background = runtime.Services.GetServices<IHostedService>().Single(service => service.GetType().Name == "OrchestrationTimeoutBackgroundService");
        await background.StopAsync(CancellationToken.None);
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<ITaskResponseTimeoutScheduler>();
            foreach (var task in waiting)
                await scheduler.CancelAsync(new TaskResponseTimeoutCancellationRequest
                {
                    OrchestrationInstanceId = task.OrchestrationInstanceId, TaskExecutionId = task.Id,
                    ActionId = task.TimeoutActionId, Lane = task.TimeoutActionLane
                });
        }
        await runtime.QueryAsync(async db =>
        {
            var sentOnUtc = DateTime.UtcNow.AddMinutes(-2);
            var tasks = await db.TaskExecutions.Where(task => topics.Contains(task.TaskKey)).ToArrayAsync();
            foreach (var task in tasks)
            {
                var attempt = await db.TaskExecutionAttempts.SingleAsync(item => item.TaskExecutionId == task.Id && item.AttemptNumber == task.LastAttemptNumber);
                var dispatch = await db.TaskDispatches.SingleAsync(item => item.Id == attempt.DispatchId);
                attempt.WaitingSinceUtc = sentOnUtc;
                dispatch.SentOnUtc = sentOnUtc;
                task.WaitingSinceUtc = null;
                task.ResponseDeadlineUtc = sentOnUtc.AddSeconds(60);
                task.TimeoutActionId = null;
            }
            await db.SaveChangesAsync();
            return true;
        });
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>()
                .ReconcileAsync(waiting[0].OrchestrationInstanceId.ToString());
            Assert.Equal("Recovered", result.Status);
            Assert.Equal(2, result.Findings.Count(finding => finding.Message.Contains("Timeout for")));
        }
        var transitions = await runtime.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
            .Where(transition => transition.TransitionType == "TaskTimedOut").ToArrayAsync());
        Assert.Equal(2, transitions.Select(transition => transition.TaskExecutionId).Distinct().Count());
        await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(task => task.TaskKey == topics[0])),
            task => task.LastAttemptNumber == 2,
            "The first timed-out task must be retried.", TimeSpan.FromSeconds(20));
        await PollingAssert.EventuallyAsync(
            () => channel.QueueDeclarePassiveAsync(topics[0]), queue => queue.MessageCount == 2,
            "Only the first task should publish a retry.", TimeSpan.FromSeconds(20));
        Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(topics[1])).MessageCount);
    }

    [Fact(Timeout = 240_000)]
    public async Task ReconciliationAfterRestartResumesPersistedCompletionOnlyOnce()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topic = $"reconcile.restart.{suffix}";
        var trigger = $"reconcile.trigger.{suffix}";
        var databaseName = infrastructure.CreateDatabaseName();
        Id instanceId;
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false);
        await using (var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure, databaseName))
        {
            var artifact = Artifact(topic, version, $"reconcile-{suffix}", [EventTrigger(trigger, version)],
                Stage("waiting", 1, MessagingTask(topic, 1, version,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(60), "Timeout"))));
            await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
            await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("test", true)), suffix);
            var task = await PollingAssert.EventuallyAsync(
                () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == topic)),
                x => x?.Status == TaskExecutionStatus.WaitingResponse,
                "Task was not dispatched", TimeSpan.FromSeconds(35));
            Assert.NotNull(task);
            instanceId = task!.OrchestrationInstanceId;
            await runtime.QueryAsync(async db =>
            {
                var savedTask = await db.TaskExecutions.SingleAsync(x => x.Id == task.Id);
                var attempt = await db.TaskExecutionAttempts.SingleAsync(x => x.TaskExecutionId == task.Id);
                var dispatch = await db.TaskDispatches.SingleAsync(x => x.TaskExecutionAttemptId == attempt.Id);
                var now = DateTime.UtcNow;
                savedTask.Status = TaskExecutionStatus.TimedOut;
                savedTask.TimedOutOnUtc = now;
                savedTask.FailedOnUtc = now;
                savedTask.WaitingSinceUtc = null;
                savedTask.ResponseDeadlineUtc = null;
                attempt.Status = TaskExecutionStatus.TimedOut;
                attempt.TimedOutOnUtc = now;
                attempt.FailedOnUtc = now;
                attempt.ErrorCode = "Timeout";
                dispatch.DispatchStatus = "TimedOut";
                await db.SaveChangesAsync();
                return true;
            });
        }

        await using (var restarted = await RealMessagingRuntimeHarness.StartAsync(infrastructure, databaseName))
        {
            await using var scope = restarted.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>();
            Assert.Equal("Recovered", (await service.ReconcileAsync(instanceId.ToString())).Status);
            await restarted.WaitForInstanceStatusAsync(suffix, OrchestrationInstanceStatus.Idle, TimeSpan.FromSeconds(20));
            var repeated = await service.ReconcileAsync(instanceId.ToString());
            Assert.NotEqual("Recovered", repeated.Status);
            var queue = await channel.QueueDeclarePassiveAsync(topic);
            Assert.Equal(1u, queue.MessageCount);
            var idleTransitions = await restarted.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
                .CountAsync(x => x.OrchestrationInstanceId == instanceId && x.TransitionType == "InstanceIdle"));
            Assert.Equal(1, idleTransitions);
        }
    }

    [Fact(Timeout = 240_000)]
    public async Task ReconciliationResumesPersistedTimeoutWhoseContinuationWasLost()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topic = $"reconcile.continuation.{suffix}";
        var trigger = $"reconcile.trigger.{suffix}";
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact(topic, version, $"reconcile-{suffix}", [EventTrigger(trigger, version)],
            Stage("waiting", 1, MessagingTask(topic, 1, version,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(60), "Timeout"))));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("test", true)), suffix);
        var task = await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == topic)),
            x => x?.Status == TaskExecutionStatus.WaitingResponse,
            "Task was not dispatched", TimeSpan.FromSeconds(35));
        Assert.NotNull(task);
        await runtime.QueryAsync(async db =>
        {
            var savedTask = await db.TaskExecutions.SingleAsync(x => x.Id == task!.Id);
            var attempt = await db.TaskExecutionAttempts.SingleAsync(x => x.TaskExecutionId == task.Id);
            var dispatch = await db.TaskDispatches.SingleAsync(x => x.TaskExecutionAttemptId == attempt.Id);
            var now = DateTime.UtcNow;
            savedTask.Status = TaskExecutionStatus.TimedOut;
            savedTask.TimedOutOnUtc = now;
            savedTask.FailedOnUtc = now;
            savedTask.WaitingSinceUtc = null;
            savedTask.ResponseDeadlineUtc = null;
            attempt.Status = TaskExecutionStatus.TimedOut;
            attempt.TimedOutOnUtc = now;
            attempt.FailedOnUtc = now;
            attempt.ErrorCode = "Timeout";
            dispatch.DispatchStatus = "TimedOut";
            await db.SaveChangesAsync();
            return true;
        });
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>()
                .ReconcileAsync(task!.OrchestrationInstanceId.ToString());
            Assert.Equal("Recovered", result.Status);
        }
        await runtime.WaitForInstanceStatusAsync(suffix, OrchestrationInstanceStatus.Idle, TimeSpan.FromSeconds(20));
        var queue = await channel.QueueDeclarePassiveAsync(topic);
        Assert.Equal(1u, queue.MessageCount);
        Assert.Equal(0u, queue.ConsumerCount);
        var transitionTypes = await runtime.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == task!.OrchestrationInstanceId)
            .Select(x => x.TransitionType).ToArrayAsync());
        Assert.Contains("InstanceReconciliationRequested", transitionTypes);
        Assert.Contains("InstanceReconciliationCompleted", transitionTypes);
    }

    [Fact(Timeout = 240_000)]
    public async Task ReconciliationReplacesConsumedTimerAndTaskTimesOutWithoutRepublishing()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topic = $"reconcile.unconsumed.{suffix}";
        var trigger = $"reconcile.trigger.{suffix}";
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact(topic, version, $"reconcile-{suffix}", [EventTrigger(trigger, version)],
            Stage("waiting", 1, MessagingTask(topic, 1, version,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(20), "Timeout"))));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("test", true)), suffix);
        var task = await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.TaskKey == topic)),
            x => x?.Status == TaskExecutionStatus.WaitingResponse && !string.IsNullOrWhiteSpace(x.TimeoutActionId),
            "Task was not dispatched", TimeSpan.FromSeconds(35));
        Assert.NotNull(task);
        var oldId = global::Mule.Ulid.Parse(task!.TimeoutActionId);
        await runtime.QueryAsync(async db =>
        {
            var action = await db.Set<global::Mule.DurableAction>().FirstAsync(x => x.Id == oldId);
            action.Status = global::Mule.DurableActionStatus.Completed;
            action.CompletedOnUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return true;
        });
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>();
            var result = await service.ReconcileAsync(task.OrchestrationInstanceId.ToString());
            Assert.Equal("Recovered", result.Status);
            Assert.Contains(result.Findings, finding => finding.Message.Contains("timeout scheduling", StringComparison.OrdinalIgnoreCase));
        }
        var repaired = await runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(x => x.Id == task.Id));
        Assert.NotEqual(task.TimeoutActionId, repaired.TimeoutActionId);
        await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().SingleAsync(x => x.Id == task.Id)),
            x => x.Status == TaskExecutionStatus.TimedOut, "Task did not time out after reconciliation", TimeSpan.FromSeconds(45));
        var queue = await channel.QueueDeclarePassiveAsync(topic);
        Assert.Equal(0u, queue.ConsumerCount);
        Assert.Equal(1u, queue.MessageCount);
        var transitions = await runtime.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
            .Where(x => x.TaskExecutionId == task.Id && x.TransitionType == "TaskTimedOut").ToArrayAsync());
        Assert.Single(transitions);
    }
}
