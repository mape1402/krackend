using Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class MessagingExtensions
    {
        internal static IServiceCollection AddMessagingDefaults(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddKrackendOrchestrationsClient()
                .AddPigeon(configuration, builder =>
                {
                    builder.SetDomain("krackend-demo");
                    builder.ScanConsumersFromAssemblies(typeof(Program).Assembly);
                    builder.SetTopologyProvisioningMode(
                        TopologyProvisioningMode.OnStartup |
                        TopologyProvisioningMode.OnPublish |
                        TopologyProvisioningMode.OnConsume);

                    builder.ConfigurePublishing(publishing =>
                    {
                        publishing.AmbientTransactionBehavior = AmbientTransactionPublishBehavior.SuppressTransaction;
                    });

                    builder.ConfigureConsumerExecution(execution =>
                    {
                        execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
                        // Let Rabbit hand every delivery to its own async handler. The
                        // service database remains the natural capacity boundary; a
                        // fixed worker semaphore here only turns unrelated commands into
                        // an application-level queue.
                        execution.MaxConcurrency = null;
                        execution.QueueCapacity = int.MaxValue;
                        execution.PrefetchCount = null;
                    });

                    builder.UseRabbitMq(rabbit =>
                    {
                        rabbit.Url = configuration["Pigeon:MessageBrokers:RabbitMq:ConnectionString"]
                            ?? "amqp://guest:guest@localhost:5672";
                    });
                }, pigeon => pigeon.ConfigureJsonOptions(options =>
                {
                    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.PropertyNameCaseInsensitive = true;
                }));

            return services;
        }
    }
}
