using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pigeon.Messaging.Consuming.Configuration;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal class PigeonMessagingAdapter : IMessagingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;

        public PigeonMessagingAdapter(IConsumingConfigurator consumingConfigurator)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
        }

        public Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.AddConsumer<JsonNode>(configuration.Topic, configuration.Version, (context, message) =>
            {
                var logger = context.Services.GetService<ILogger<PigeonMessagingAdapter>>();
                logger.LogInformation("Prepare for intake!!");

                return Task.CompletedTask;
            });

            return Task.CompletedTask;   
        }
    }
}
