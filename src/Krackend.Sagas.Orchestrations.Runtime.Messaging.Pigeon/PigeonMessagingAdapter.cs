using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;
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
            _consumingConfigurator.AddConsumer<JsonNode>(configuration.Topic, configuration.Version, async (context, message) =>
            {
                var intake = context.Services.GetRequiredService<IIntakeBuffer>();
                var metadataAccessor = context.Services.GetRequiredService<IInstanceMetadataAccessor>();

                var workItem = new WorkItem
                {
                    ArtifactId = configuration.ArtifactId,
                    IngressKind = configuration.IngressKind,
                    IngressTransport = configuration.IngressTransport,
                    Payload = message,
                    Metadata = metadataAccessor.Get()
                };

                await intake.EnqueueWorkAsync(workItem, cancellationToken);
            });

            return Task.CompletedTask;   
        }
    }
}
