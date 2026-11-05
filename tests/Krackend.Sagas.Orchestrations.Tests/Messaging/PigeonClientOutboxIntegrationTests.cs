namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mule;
using Mule.InMemory;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.InMemory;
using Pigeon.Messaging.Producing;
using SquirrelBox;
using SquirrelBox.InMemory;
using SquirrelBox.Messaging;
using SquirrelBox.Messaging.Pigeon;
using SquirrelBox.Mule;

public sealed class PigeonClientOutboxIntegrationTests
{
    private const string DuplicateRequestTopic = "krackend.test.duplicate.request";

    [Fact]
    public async Task CompletedDuplicateSkipsHandlerAndPublishesSuccessThroughRealPigeonPipeline()
    {
        const string replyTopic = "krackend.test.duplicate.reply";
        var replies = new ConcurrentQueue<(OrchestrationExecutionResultMetadata Result, OrchestrationMessageMetadata Message)>();
        var executions = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSquirrelBox().UseInMemory();
        services.AddMule(mule => mule.UseInMemory());
        services.AddSquirrelBoxMessaging(options =>
        {
            options.UseMessageIdWhenKeyIsMissing = false;
            options.ScanAssemblyContaining<DuplicateInboxPolicy>();
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pigeon:Domain"] = "Krackend.Client.Duplicate.Tests"
        }).Build();
        var pigeon = services.AddPigeon(configuration, settings =>
        {
            settings.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
            settings.UseInMemoryBroker();
        });
        pigeon.AddConsumeHandler<JsonObject>(DuplicateRequestTopic, SemanticVersion.Default, "worker", async (context, _) =>
        {
            Interlocked.Increment(ref executions);
            var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
            client.Begin<JsonObject>();
            try { await client.ReportSuccessAsync<JsonObject, JsonObject>(new JsonObject { ["accepted"] = true }); }
            finally { client.Close(); }
        });
        pigeon.AddConsumeHandler<JsonObject>(replyTopic, SemanticVersion.Default, "runtime", (context, _) =>
        {
            replies.Enqueue((
                context.GetMetadata<OrchestrationExecutionResultMetadata>(OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey),
                context.GetMetadata<OrchestrationMessageMetadata>(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey)));
            return Task.CompletedTask;
        });
        var clientBuilder = services.AddKrackendOrchestrationsClient();
        Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(clientBuilder);
        services.AddSquirrelBoxPigeon();

        await using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hostedServices) await service.StartAsync(CancellationToken.None);
        try
        {
            using var scope = provider.CreateScope();
            var metadata = new OrchestrationMessageMetadata
            {
                SagaId = "saga-1", OrchestrationInstanceId = "instance-1", TaskExecutionId = "task-1", Attempt = 2,
                ReplyAddress = new OrchestrationReplyAddress
                {
                    Transport = OrchestrationTransportNames.Messaging,
                    SettingsPayload = "{\"topic\":\"krackend.test.duplicate.reply\",\"version\":\"1.0.0\"}"
                }
            };
            scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>().Set(metadata);
            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await producer.PublishAsync(new JsonObject { ["orderId"] = "order-1" }, DuplicateRequestTopic);
            await WaitUntilAsync(() => replies.Count == 1 &&
                broker.Deliveries.Count(x => x.Topic == DuplicateRequestTopic && x.Completed) == 1);
            await producer.PublishAsync(new JsonObject { ["orderId"] = "order-1" }, DuplicateRequestTopic);
            await WaitUntilAsync(() => replies.Count == 2 &&
                broker.Deliveries.Count(x => x.Topic == DuplicateRequestTopic && x.Completed) == 2);

            Assert.Equal(1, Volatile.Read(ref executions));
            var messages = replies.ToArray();
            Assert.True(messages[0].Result.Succeeded);
            Assert.True(messages[1].Result.Succeeded);
            Assert.Equal("AlreadyProcessed", messages[1].Result.Status);
            Assert.Equal("saga-1", messages[1].Message.SagaId);
            Assert.Equal("instance-1", messages[1].Message.OrchestrationInstanceId);
            Assert.Equal("task-1", messages[1].Message.TaskExecutionId);
            Assert.Equal(2, messages[1].Message.Attempt);
            Assert.Equal(metadata.ReplyAddress.Transport, messages[1].Message.ReplyAddress.Transport);
            Assert.Equal(metadata.ReplyAddress.SettingsPayload, messages[1].Message.ReplyAddress.SettingsPayload);
        }
        finally
        {
            foreach (var service in hostedServices.Reverse()) await service.StopAsync(CancellationToken.None);
        }
    }

    public sealed class DuplicateInboxPolicy : InboxMessagePolicyProfile
    {
        public override void Configure(InboxMessagePolicyProfileBuilder builder) => builder.ForTopic(DuplicateRequestTopic);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("The expected Pigeon callback was not received.");
            await Task.Delay(25);
        }
    }

    [Theory]
    [InlineData(PigeonConsumeOutcomeKind.Rejected, false)]
    [InlineData(PigeonConsumeOutcomeKind.AlreadyProcessed, true)]
    public async Task ObserverCallbackPassesThroughHostOutboxAndIsEventuallyDelivered(PigeonConsumeOutcomeKind kind, bool succeeded)
    {
        const string replyTopic = "krackend.test.outbox.reply";
        var reply = new TaskCompletionSource<OrchestrationExecutionResultMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<MuleSettings>(settings =>
        {
            settings.ImmediateDispatch = true;
            settings.DispatchInterval = TimeSpan.FromMilliseconds(20);
            settings.RetryDelay = TimeSpan.FromMilliseconds(20);
            settings.CleanupMode = MuleCleanupMode.Disabled;
        });
        services.AddSquirrelBox().UseInMemory();
        services.AddMule(mule => mule.UseInMemory().AddActionsFromAssemblyContaining<SquirrelBoxOutboxMuleAction>());
        services.AddSquirrelBoxMessaging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pigeon:Domain"] = "Krackend.Client.Outbox.Tests"
        }).Build();
        var pigeon = services.AddPigeon(configuration, settings =>
        {
            settings.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
            settings.UseInMemoryBroker();
        });
        pigeon.AddConsumeHandler<JsonObject>(replyTopic, SemanticVersion.Default, "reply-consumer", (context, _) =>
        {
            reply.TrySetResult(context.GetMetadata<OrchestrationExecutionResultMetadata>(
                OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey));
            return Task.CompletedTask;
        });
        var client = services.AddKrackendOrchestrationsClient();
        Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(client);
        Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(client);
        services.AddSquirrelBoxPigeon(options =>
        {
            options.EnableOutbox = true;
            options.OutboxPredicate = context => string.Equals(context.Route?.Topic, replyTopic, StringComparison.Ordinal);
        });

        await using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hostedServices)
            await service.StartAsync(CancellationToken.None);

        try
        {
            using var scope = provider.CreateScope();
            var observers = scope.ServiceProvider.GetServices<IConsumeDecisionObserver>()
                .Where(observer => observer.GetType().Name == "KrackendPigeonConsumeDecisionObserver")
                .ToArray();
            var observer = Assert.Single(observers);
            var context = new ConsumeContext();
            var metadata = new OrchestrationMessageMetadata
            {
                SagaId = "saga-1",
                OrchestrationInstanceId = "instance-1",
                TaskExecutionId = "task-1",
                Attempt = 1,
                ReplyAddress = new OrchestrationReplyAddress
                {
                    Transport = OrchestrationTransportNames.Messaging,
                    SettingsPayload = "{\"topic\":\"krackend.test.outbox.reply\",\"version\":\"1.0.0\"}"
                }
            };
            var field = typeof(ConsumeContext).GetField("_metadata", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((ConcurrentDictionary<string, object>)field.GetValue(context)!)[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] = metadata;
            var decision = new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip, "rejected")
            {
                Outcome = new PigeonConsumeOutcome
                {
                    Kind = kind,
                    Failure = succeeded ? null : new PigeonConsumeFailure { Code = "test.rejected", IsRetryable = false }
                }
            };
            await observer.ObserveAsync(new ConsumeDecisionObservation(context, decision), CancellationToken.None);

            var received = await reply.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(succeeded, received.Succeeded);
            Assert.Equal(succeeded ? "AlreadyProcessed" : "Rejected", received.Status);
            Assert.Equal(succeeded ? null : "test.rejected", received.ErrorCode);
            var envelope = Assert.Single(await scope.ServiceProvider.GetRequiredService<IOutboxStore>()
                .QueryAsync(new OutboxQuery { Transport = "pigeon" }));
            Assert.Equal(replyTopic, envelope.Destination);
        }
        finally
        {
            foreach (var service in hostedServices.Reverse())
                await service.StopAsync(CancellationToken.None);
        }
    }
}
