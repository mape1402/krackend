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





