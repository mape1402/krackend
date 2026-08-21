namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultMessagingReplyAddressSettingsSerializer : IMessagingReplyAddressSettingsSerializer
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public MessagingReplyAddressSettings Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Messaging reply address settings payload cannot be empty.", nameof(payload));
        }

        return JsonSerializer.Deserialize<MessagingReplyAddressSettings>(payload, _serializerOptions)
            ?? throw new InvalidOperationException("Messaging reply address settings payload could not be deserialized.");
    }
}
