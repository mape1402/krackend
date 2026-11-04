namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mule;
using Mule.InMemory;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.InMemory;
using SquirrelBox;
using SquirrelBox.InMemory;
using SquirrelBox.Messaging;
using SquirrelBox.Messaging.Pigeon;
using SquirrelBox.Mule;

public sealed class PigeonClientOutboxIntegrationTests
{
    [Fact]
    public async Task ObserverCallbackPassesThroughHostOutboxAndIsEventuallyDelivered()
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
                    Kind = PigeonConsumeOutcomeKind.Rejected,
                    Failure = new PigeonConsumeFailure { Code = "test.rejected", IsRetryable = false }
                }
            };
            await observer.ObserveAsync(new ConsumeDecisionObservation(context, decision), CancellationToken.None);

            var received = await reply.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(received.Succeeded);
            Assert.Equal("test.rejected", received.ErrorCode);
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
