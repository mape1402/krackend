namespace Krackend.Sagas.Orchestrations.Tests.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using static Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure.RealMessagingArtifactFactory;

[Collection(RuntimeRealInfrastructureCollection.Name)]
public sealed class UnconsumedQueueTimeoutReproductionTests(RuntimeRealInfrastructureFixture infrastructure)
{
    [Fact(Timeout = 240_000)]
    public async Task ParallelUnconsumedCommandsEachTimeoutAndRetryIndependently()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topics = new[] { $"repro.retry.{suffix}.one", $"repro.retry.{suffix}.two" };
        var trigger = $"repro.retry.trigger.{suffix}";
        var group = Id.New();
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        foreach (var topic in topics)
            await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact($"repro-retry-{suffix}", version, $"repro-retry-{suffix}", [EventTrigger(trigger, version)],
            StageWithGraph("parallel-retry", 1, [new ParallelGroupArtifact(group, ParallelJoinPolicy.WaitAll, null)], [],
                topics.Select((topic, index) => MessagingTask(topic, index + 1, version,
                    retryPolicy: RetryPolicy(1, "UnconsumedTimeout"),
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(index == 0 ? 5 : 30), "UnconsumedTimeout"),
                    executionMode: TaskExecutionMode.Parallel, parallelGroupId: group)).ToArray()));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("repro", true)), $"repro-{suffix}");
        await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(t => topics.Contains(t.TaskKey)).ToArrayAsync()),
            tasks => tasks.Length == 2 && tasks.Any(task => task.TaskKey == topics[0] && task.LastAttemptNumber >= 2) &&
                tasks.Any(task => task.TaskKey == topics[1] && task.Status == TaskExecutionStatus.WaitingResponse),
            "The first task should retry while the other parallel task is still waiting.", TimeSpan.FromSeconds(25));
        await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
                .Where(t => t.TransitionType == "TaskTimedOut").Select(t => t.TaskExecutionId).ToArrayAsync()),
            transitions => transitions.Where(id => id.HasValue).Select(id => id!.Value).Distinct().Count() == 2,
            "Both parallel tasks should independently time out even when the first is retrying.", TimeSpan.FromSeconds(60));
        foreach (var topic in topics)
        {
            var queue = await channel.QueueDeclarePassiveAsync(topic);
            Assert.Equal(0u, queue.ConsumerCount);
        }
    }

    [Fact(Timeout = 300_000)]
    public async Task ThreeParallelCommandsWithoutConsumersMustTimeOut()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var version = new SemanticVersion(1, 0, 0);
        var topic = $"repro.unconsumed.{suffix}";
        var topics = Enumerable.Range(1, 3).Select(i => $"{topic}.{i}").ToArray();
        var parallelGroupId = Id.New();
        var trigger = $"repro.trigger.{suffix}";
        await using var connection = await new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMqConnectionString) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        foreach (var queueTopic in topics)
            await channel.QueueDeclareAsync(queueTopic, durable: false, exclusive: false, autoDelete: false);
        await using var runtime = await RealMessagingRuntimeHarness.StartAsync(infrastructure);
        var artifact = Artifact(topic, version, $"repro-{suffix}", [EventTrigger(trigger, version)],
            StageWithGraph("unconsumed", 1,
                [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, null)], [],
                topics.Select((queueTopic, index) => MessagingTask(queueTopic, index + 1, version,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(60), "UnconsumedQueueTimeout"),
                    executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId)).ToArray()));
        await runtime.DeployAndWaitReadyAsync(CreatePackage(artifact));
        var correlation = $"repro-{suffix}";
        await runtime.PublishTriggerAsync(trigger, version.ToString(), BusinessPayload(("repro", true)), correlation);
        try {
        await PollingAssert.EventuallyAsync(
            () => runtime.QueryAsync(db => db.TaskExecutions.AsNoTracking().Where(t => topics.Contains(t.TaskKey)).Select(t => t.Status).ToArrayAsync()),
            tasks => tasks.Length == 3 && tasks.All(status => status == TaskExecutionStatus.TimedOut),
            "Command with no consumer never reached TimedOut", TimeSpan.FromSeconds(120));
        } finally {
            var state = await runtime.QueryAsync(async db => new {
                Tasks = await db.TaskExecutions.AsNoTracking().ToArrayAsync(),
                Instances = await db.OrchestrationInstances.AsNoTracking().ToArrayAsync(),
                Dispatches = await db.TaskDispatches.AsNoTracking().ToArrayAsync(),
                Actions = await db.Set<Mule.DurableAction>().AsNoTracking().ToArrayAsync()
            });
            Console.WriteLine("REPRO_STATE=" + System.Text.Json.JsonSerializer.Serialize(state));
            foreach (var queueTopic in topics) {
                var observedQueue = await channel.QueueDeclarePassiveAsync(queueTopic);
                Console.WriteLine($"REPRO_QUEUE topic={queueTopic} messages={observedQueue.MessageCount} consumers={observedQueue.ConsumerCount}");
            }
        }
        foreach (var queueTopic in topics) {
            var queue = await channel.QueueDeclarePassiveAsync(queueTopic);
            Assert.Equal(0u, queue.ConsumerCount);
            Assert.Equal(1u, queue.MessageCount);
        }
        var timeouts = await runtime.QueryAsync(db => db.ExecutionTransitions.AsNoTracking()
            .Where(transition => transition.TransitionType == "TaskTimedOut")
            .ToArrayAsync());
        Assert.Equal(3, timeouts.Length);
        Assert.Equal(3, timeouts.Select(transition => transition.TaskExecutionId).Distinct().Count());
    }
}





