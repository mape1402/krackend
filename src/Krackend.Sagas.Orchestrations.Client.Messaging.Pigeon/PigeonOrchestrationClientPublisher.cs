namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using global::Pigeon.Messaging.Producing;
using global::Pigeon.Messaging.Contracts;

internal sealed class PigeonOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    private readonly IProducer _producer;
    private readonly IMessagingReplyAddressSettingsSerializer _settingsSerializer;

    public PigeonOrchestrationClientPublisher(
        IProducer producer,
        IMessagingReplyAddressSettingsSerializer settingsSerializer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _settingsSerializer = settingsSerializer ?? throw new ArgumentNullException(nameof(settingsSerializer));
    }

    public async Task PublishAsync(object payload, OrchestrationReplyAddress address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!string.Equals(address.Transport, OrchestrationTransportNames.Messaging, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Pigeon cannot publish orchestration replies for transport '{address.Transport}'.");
        }

        var settings = _settingsSerializer.Deserialize(address.SettingsPayload);
        var version = SemanticVersion.Parse(settings.Version);
        await _producer.PublishAsync(payload, settings.Topic, version, cancellationToken);
    }
}
