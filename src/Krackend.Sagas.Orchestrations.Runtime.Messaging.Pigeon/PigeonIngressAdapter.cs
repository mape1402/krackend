using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;
using global::Pigeon.Messaging.Consuming.Configuration;
using global::Pigeon.Messaging.Contracts;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal class PigeonIngressAdapter : IMessagingIngressAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;

        public PigeonIngressAdapter(IConsumingConfigurator consumingConfigurator)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
        }

        public Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var version = SemanticVersion.Parse(configuration.Version);
            _consumingConfigurator.AddConsumer<JsonNode>(configuration.Topic, version, "Default", async (context, message) =>
            {
                var intake = context.Services.GetRequiredService<IIntakeBuffer>();
                var messageMetadataAccessor = context.Services.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
                var resultMetadataAccessor = context.Services.GetRequiredService<IOrchestrationExecutionResultMetadataAccessor>();

                var workItem = new WorkItem
                {
                    ArtifactId = configuration.ArtifactId,
                    IngressKind = configuration.IngressKind,
                    IngressTransport = configuration.IngressTransport,
                    Payload = message,
                    MessageMetadata = messageMetadataAccessor.Get(),
                    ExecutionResultMetadata = resultMetadataAccessor.Get()
                };

                await intake.EnqueueWorkAsync(workItem, cancellationToken);

                await context.CompleteAsync(cancellationToken);
            });

            return Task.CompletedTask;   
        }
    }
}
