using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using global::Pigeon.Messaging;
using global::Pigeon.Messaging.Consuming.Management;
using global::Pigeon.Messaging.Consuming.Configuration;
using global::Pigeon.Messaging.Contracts;
using global::Pigeon.Messaging.Topology;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal class PigeonIngressAdapter : IMessagingIngressAdapter
    {
        private const string SubscriptionName = "Default";
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IPigeonIngressConsumerRegistry _consumerRegistry;
        private readonly IMessagingIngressConfigurationSelector _ingressSelector;
        private readonly ILogger<PigeonIngressAdapter> _logger;
        private readonly ITopologyProvisioningService _topologyProvisioning;
        private readonly IOptions<GlobalSettings> _settings;
        private readonly IPigeonConsumerReadiness _readiness;
        private readonly IOptions<PigeonConsumerReadinessOptions> _readinessOptions;

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            IMessagingIngressConfigurationSelector ingressSelector,
            ILogger<PigeonIngressAdapter> logger)
            : this(
                consumingConfigurator,
                consumerRegistry,
                ingressSelector,
                logger,
                null)
        {
        }

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            IMessagingIngressConfigurationSelector ingressSelector,
            ILogger<PigeonIngressAdapter> logger,
            ITopologyProvisioningService topologyProvisioning)
            : this(consumingConfigurator, consumerRegistry, ingressSelector, logger, topologyProvisioning, null, null)
        {
        }

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            IMessagingIngressConfigurationSelector ingressSelector,
            ILogger<PigeonIngressAdapter> logger,
            ITopologyProvisioningService topologyProvisioning,
            IOptions<GlobalSettings> settings)
            : this(consumingConfigurator, consumerRegistry, ingressSelector, logger, topologyProvisioning, settings, null)
        {
        }

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            IMessagingIngressConfigurationSelector ingressSelector,
            ILogger<PigeonIngressAdapter> logger,
            ITopologyProvisioningService topologyProvisioning,
            IOptions<GlobalSettings> settings,
            IPigeonConsumerReadiness readiness)
            : this(consumingConfigurator, consumerRegistry, ingressSelector, logger, topologyProvisioning, settings, readiness, null)
        {
        }

        public PigeonIngressAdapter(
            IConsumingConfigurator consumingConfigurator,
            IPigeonIngressConsumerRegistry consumerRegistry,
            IMessagingIngressConfigurationSelector ingressSelector,
            ILogger<PigeonIngressAdapter> logger,
            ITopologyProvisioningService topologyProvisioning,
            IOptions<GlobalSettings> settings,
            IPigeonConsumerReadiness readiness = null,
            IOptions<PigeonConsumerReadinessOptions> readinessOptions = null)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _consumerRegistry = consumerRegistry ?? throw new ArgumentNullException(nameof(consumerRegistry));
            _ingressSelector = ingressSelector ?? throw new ArgumentNullException(nameof(ingressSelector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _topologyProvisioning = topologyProvisioning;
            _settings = settings;
            _readiness = readiness;
            _readinessOptions = readinessOptions;
        }

        public async Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration.IsStaged && (_settings?.Value is null ||
                _settings.Value.ConsumerExecution?.AcknowledgementMode != MessageAcknowledgementMode.OnHandlerSuccess))
                throw new IngressStandupConfigurationException("Staged rollout requires Pigeon acknowledgement mode OnHandlerSuccess.");
            var version = SemanticVersion.Parse(configuration.Version);
            var endpointKey = BuildEndpointKey(configuration.Topic, configuration.Version, SubscriptionName);
            var registration = new PigeonIngressConsumerRegistration(
                configuration.ConnectorId,
                endpointKey,
                configuration.Topic,
                configuration.Version,
                SubscriptionName);

            var attached = _consumerRegistry.TryAttach(registration, out var shouldRegisterEndpoint);
            if (!attached)
            {
                if (configuration.IsStaged && _readiness is not null)
                    await WaitUntilReadyAsync(configuration, cancellationToken);
                return;
            }

            if (!shouldRegisterEndpoint)
            {
                try
                {
                    if (configuration.IsStaged && _readiness is not null)
                        await WaitUntilReadyAsync(configuration, cancellationToken);
                }
                catch
                {
                    _consumerRegistry.ForgetConnector(configuration.ConnectorId);
                    throw;
                }
                return;
            }

            var registeredEndpoint = false;
            try
            {
                if (_topologyProvisioning is not null)
                {
                    await _topologyProvisioning.EnsureConsumeTopologyAsync(
                        new ConsumerEndpoint(configuration.Topic, SubscriptionName),
                        cancellationToken);
                }

                _consumingConfigurator.AddConsumer<JsonNode>(configuration.Topic, version, SubscriptionName, async (context, message) =>
                {
                    var intake = context.Services.GetRequiredService<IIntakeBuffer>();
                    var messageMetadataAccessor = context.Services.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
                    var propagationMetadataAccessor = context.Services.GetRequiredService<IOrchestrationPropagationMetadataAccessor>();
                    var resultMetadataAccessor = context.Services.GetRequiredService<IOrchestrationExecutionResultMetadataAccessor>();
                    using var reader = context.Services.GetRequiredService<IGetAllIngressConfigurationsAccessor>();
                    var configurations = new List<IngressConfiguration>();

                    IngressConfigurationReadingResult dataset;
                    do
                    {
                        dataset = await reader.ReadAsync(context.CancellationToken);
                        configurations.AddRange(dataset.Configurations);
                    }
                    while (dataset.HasMoreItems);

                    var matchingIngress = _ingressSelector
                        .SelectMatching(configurations, configuration.Topic, configuration.Version)
                        .ToArray();
                    var processableIngress = matchingIngress
                        .Where(ingress => ingress.MessageDisposition == IngressMessageDisposition.Process)
                        .ToArray();
                    if (processableIngress.Length == 0 && matchingIngress.Any(ingress =>
                            ingress.IngressKind == IngressKind.Trigger &&
                            ingress.MessageDisposition == IngressMessageDisposition.Reject))
                    {
                        await context.RejectAsync(
                            new IngressMessageRejectedException(
                                $"The matching orchestration trigger for '{configuration.Topic}' v{configuration.Version} is deprecated."),
                            context.CancellationToken);
                        return;
                    }

                    if (processableIngress.Length == 0)
                    {
                        // A staged connector can start before its artifact is routable.
                        // Never acknowledge a delivery that has no active destination.
                        // Let execution interceptors record the failure before
                        // Pigeon requests redelivery in OnHandlerSuccess mode.
                        throw new InvalidOperationException(
                            $"No active orchestration ingress matches '{configuration.Topic}' v{configuration.Version}.");
                    }

                    foreach (var ingress in processableIngress)
                    {
                        await intake.EnqueueWorkAsync(new WorkItem
                        {
                            ArtifactId = ingress.ArtifactId,
                            IngressKind = ingress.IngressKind,
                            IngressTransport = ingress.IngressTransport,
                            Payload = message?.DeepClone(),
                            MessageMetadata = messageMetadataAccessor.Get(),
                            PropagationMetadata = propagationMetadataAccessor.Get(),
                            ExecutionResultMetadata = resultMetadataAccessor.Get()
                        }, context.CancellationToken);
                    }

                    await context.CompleteAsync(context.CancellationToken);
                });
                registeredEndpoint = true;
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

            if (configuration.IsStaged && _readiness is not null)
            {
                try
                {
                    await WaitUntilReadyAsync(configuration, cancellationToken);
                }
                catch
                {
                    if (registeredEndpoint)
                        await DisconnectAsync(configuration.ConnectorId, CancellationToken.None);
                    else
                        _consumerRegistry.ForgetConnector(configuration.ConnectorId);
                    throw;
                }
            }
        }

        private async Task WaitUntilReadyAsync(MessagingConfiguration configuration, CancellationToken cancellationToken)
        {
            var seconds = Math.Max(1, _readinessOptions?.Value.TimeoutSeconds ?? 60);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
            try
            {
                await _readiness.WaitUntilReadyAsync(configuration.Topic, configuration.Version, SubscriptionName, timeout.Token)
                    .WaitAsync(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Pigeon consumer readiness timed out after {seconds} seconds.", exception);
            }
        }

        public Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default)
        {
            _consumerRegistry.RemoveConnector(connectorId, registration =>
            {
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
            });

            return Task.CompletedTask;
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
