using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal sealed class DefaultRuntimeIngressConfigurationProjector : IRuntimeIngressConfigurationProjector
    {
        private readonly IRuntimeArtifactSerializer _artifactSerializer;
        private readonly IRuntimeIngressConfigurationRepository _repository;
        private readonly IMessagingConfigurationSerializer _messagingConfigurationSerializer;
        private readonly IBackchannelMessagingTopicFormatter _backchannelTopicFormatter;
        private readonly IRuntimeIngressConfigurationKeyBuilder _keyBuilder;

        public DefaultRuntimeIngressConfigurationProjector(
            IRuntimeArtifactSerializer artifactSerializer,
            IRuntimeIngressConfigurationRepository repository,
            IMessagingConfigurationSerializer messagingConfigurationSerializer,
            IBackchannelMessagingTopicFormatter backchannelTopicFormatter,
            IRuntimeIngressConfigurationKeyBuilder keyBuilder)
        {
            _artifactSerializer = artifactSerializer ?? throw new ArgumentNullException(nameof(artifactSerializer));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _messagingConfigurationSerializer = messagingConfigurationSerializer ?? throw new ArgumentNullException(nameof(messagingConfigurationSerializer));
            _backchannelTopicFormatter = backchannelTopicFormatter ?? throw new ArgumentNullException(nameof(backchannelTopicFormatter));
            _keyBuilder = keyBuilder ?? throw new ArgumentNullException(nameof(keyBuilder));
        }

        public async Task ProjectAsync(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            if (artifact is null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            var orchestrationArtifact = _artifactSerializer.Deserialize(artifact.ArtifactPayload.ToJsonString());
            var now = DateTime.UtcNow;
            var triggerConfigurations = orchestrationArtifact.TriggerBindings
                .Where(x => x.IsEnabled)
                .SelectMany(x => BuildTriggerConfigurations(artifact.Id, x, now))
                .ToArray();
            if (triggerConfigurations.Length == 0)
            {
                throw new IngressProjectionConfigurationException(
                    $"Artifact '{artifact.Id}' does not contain any enabled messaging trigger ingress configuration.");
            }

            var configurations = triggerConfigurations
                .Append(BuildBackchannelConfiguration(artifact.Id, orchestrationArtifact, artifact.Version, now))
                .ToArray();

            await _repository.UpsertForArtifactAsync(artifact.Id, configurations, cancellationToken);
        }

        private IEnumerable<RuntimeIngressConfiguration> BuildTriggerConfigurations(
            Id artifactId,
            TriggerBindingArtifact trigger,
            DateTime now)
        {
            if (trigger.TriggerChannel is not EventTriggerChannelArtifact eventTrigger)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(eventTrigger.Topic))
            {
                throw new IngressProjectionConfigurationException(
                    $"Trigger '{trigger.Id}' does not define a messaging topic.");
            }

            yield return new RuntimeIngressConfiguration
            {
                Id = Id.New(),
                RuntimeOrchestrationArtifactId = artifactId,
                ConfigurationKey = _keyBuilder.BuildTriggerKey(trigger),
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = _messagingConfigurationSerializer.Serialize(new MessagingConfiguration
                {
                    Topic = eventTrigger.Topic,
                    Version = eventTrigger.Version.ToString()
                }),
                IsActive = true,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            };
        }

        private RuntimeIngressConfiguration BuildBackchannelConfiguration(
            Id artifactId,
            OrchestrationArtifact artifact,
            SemanticVersion version,
            DateTime now)
            => new()
            {
                Id = Id.New(),
                RuntimeOrchestrationArtifactId = artifactId,
                ConfigurationKey = _keyBuilder.BuildBackchannelKey(IngressTransport.Messaging),
                IngressKind = IngressKind.Backchannel,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = _messagingConfigurationSerializer.Serialize(new MessagingConfiguration
                {
                    Topic = _backchannelTopicFormatter.Format(artifact),
                    Version = version.ToString()
                }),
                IsActive = true,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            };
    }
}
