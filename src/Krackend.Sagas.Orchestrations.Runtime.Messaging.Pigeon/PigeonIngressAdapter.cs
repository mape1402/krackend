using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using global::Pigeon.Messaging.Consuming.Configuration;
using global::Pigeon.Messaging.Contracts;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal class PigeonIngressAdapter : IMessagingIngressAdapter
    {
        private const string SubscriptionName = "Default";
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IPigeonIngressConsumerRegistry _consumerRegistry;
        private readonly ILogger<PigeonIngressAdapter> _logger;

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            ILogger<PigeonIngressAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _consumerRegistry = consumerRegistry ?? throw new ArgumentNullException(nameof(consumerRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var version = SemanticVersion.Parse(configuration.Version);
            var endpointKey = BuildEndpointKey(configuration.Topic, configuration.Version, SubscriptionName);
            var registration = new PigeonIngressConsumerRegistration(
                configuration.ConnectorId,
                endpointKey,
                configuration.Topic,
                configuration.Version,
                SubscriptionName);

            if (!_consumerRegistry.TryAttach(registration, out var shouldRegisterEndpoint) ||
                !shouldRegisterEndpoint)
            {
                return Task.CompletedTask;
            }

            try
            {
                _consumingConfigurator.AddConsumer<JsonNode>(configuration.Topic, version, SubscriptionName, async (context, message) =>
                {
                    var intake = context.Services.GetRequiredService<IIntakeBuffer>();
                    var serializer = context.Services.GetRequiredService<IMessagingConfigurationSerializer>();
                    var messageMetadataAccessor = context.Services.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
                    var resultMetadataAccessor = context.Services.GetRequiredService<IOrchestrationExecutionResultMetadataAccessor>();
                    using var reader = context.Services.GetRequiredService<IGetAllIngressConfigurationsAccessor>();

                    IngressConfigurationReadingResult dataset;
                    do
                    {
                        dataset = await reader.ReadAsync(cancellationToken);
                        foreach (var ingress in dataset.Configurations.Where(x => Matches(x, serializer, configuration.Topic, configuration.Version)))
                        {
                            await intake.EnqueueWorkAsync(new WorkItem
                            {
                                ArtifactId = ingress.ArtifactId,
                                IngressKind = ingress.IngressKind,
                                IngressTransport = ingress.IngressTransport,
                                Payload = message?.DeepClone(),
                                MessageMetadata = messageMetadataAccessor.Get(),
                                ExecutionResultMetadata = resultMetadataAccessor.Get()
                            }, cancellationToken);
                        }
                    }
                    while (dataset.HasMoreItems);

                    await context.CompleteAsync(cancellationToken);
                });
            }
            catch (InvalidOperationException ex) when (IsAlreadyRegistered(ex))
            {
                _logger.LogInformation(
                    "Pigeon consumer for topic '{Topic}' version '{Version}' subscription '{Subscription}' already exists.",
                    configuration.Topic,
                    configuration.Version,
                    SubscriptionName);
            }
            catch
            {
                _consumerRegistry.ForgetConnector(configuration.ConnectorId);
                throw;
            }

            return Task.CompletedTask;   
        }

        public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
        {
            if (!_consumerRegistry.TryDetach(
                connectorId,
                out var registration,
                out var shouldRemoveEndpoint) ||
                !shouldRemoveEndpoint)
            {
                return Task.CompletedTask;
            }

            var version = SemanticVersion.Parse(registration.Version);
            try
            {
                _consumingConfigurator.RemoveConsumer(registration.Topic, version);
            }
            catch (InvalidOperationException ex) when (IsAlreadyRemoved(ex))
            {
                _logger.LogInformation(
                    "Pigeon consumer for topic '{Topic}' version '{Version}' subscription '{Subscription}' was already removed.",
                    registration.Topic,
                    registration.Version,
                    registration.Subscription);
            }

            return Task.CompletedTask;
        }

        private static bool Matches(
            IngressConfiguration ingress,
            IMessagingConfigurationSerializer serializer,
            string topic,
            string version)
        {
            if (ingress.IngressTransport != IngressTransport.Messaging)
            {
                return false;
            }

            var settings = serializer.Deserialize(ingress.SettingsPayload);
            return string.Equals(settings.Topic, topic, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(settings.Version, version, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildEndpointKey(string topic, string version, string subscription)
            => $"{topic.Trim()}|{version.Trim()}|{subscription.Trim()}".ToLowerInvariant();

        private static bool IsAlreadyRegistered(Exception exception)
            => exception.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase);

        private static bool IsAlreadyRemoved(Exception exception)
            => exception.Message.Contains("not registered", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }
}
