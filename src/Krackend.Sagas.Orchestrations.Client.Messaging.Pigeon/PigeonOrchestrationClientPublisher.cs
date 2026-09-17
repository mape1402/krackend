namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using global::Pigeon.Messaging.Producing;
using global::Pigeon.Messaging.Contracts;
using System.Text.Json.Nodes;

internal sealed class PigeonOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    private readonly IProducer _producer;
    private readonly IMessagingReplyAddressSettingsSerializer _settingsSerializer;
    private readonly IOrchestrationExecutionResultMetadataAccessor _resultMetadataAccessor;

    public PigeonOrchestrationClientPublisher(
        IProducer producer,
        IMessagingReplyAddressSettingsSerializer settingsSerializer,
        IOrchestrationExecutionResultMetadataAccessor resultMetadataAccessor)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _settingsSerializer = settingsSerializer ?? throw new ArgumentNullException(nameof(settingsSerializer));
        _resultMetadataAccessor = resultMetadataAccessor ?? throw new ArgumentNullException(nameof(resultMetadataAccessor));
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
        await _producer.PublishAsync(PreparePayload(payload), settings.Topic, version, cancellationToken);
    }

    private object PreparePayload(object payload)
    {
        if (payload is not null)
        {
            return payload;
        }

        var resultMetadata = _resultMetadataAccessor.Get();
        if (resultMetadata is not null)
        {
            resultMetadata.Metadata[OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey] =
                JsonValue.Create(true);
        }

        return new JsonObject();
    }
}
