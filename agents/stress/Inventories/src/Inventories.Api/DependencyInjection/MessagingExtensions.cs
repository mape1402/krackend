using Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using System.Diagnostics.CodeAnalysis;

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
                    builder.ScanConsumersFromAssemblies(typeof(Program).Assembly);
                    builder.SetTopologyProvisioningMode(
                        TopologyProvisioningMode.OnStartup |
                        TopologyProvisioningMode.OnPublish |
                        TopologyProvisioningMode.OnConsume);

                    builder.ConfigurePublishing(publishing =>
                    {
                        publishing.AmbientTransactionBehavior = AmbientTransactionPublishBehavior.SuppressTransaction;
                    });

                    builder.UseRabbitMq(rabbit =>
                    {
                        rabbit.Url = configuration["Pigeon:MessageBrokers:RabbitMq:ConnectionString"]
                            ?? "amqp://guest:guest@localhost:5672";
                    });
                });

            return services;
        }
    }
}
