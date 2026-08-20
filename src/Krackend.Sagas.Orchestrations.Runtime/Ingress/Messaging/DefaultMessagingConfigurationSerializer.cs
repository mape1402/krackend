using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    internal sealed class DefaultMessagingConfigurationSerializer : IMessagingConfigurationSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public MessagingConfiguration Deserialize(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new ArgumentException("Messaging configuration payload cannot be empty.", nameof(rawJson));
            }

            return JsonSerializer.Deserialize<MessagingConfiguration>(rawJson, SerializerOptions)
                ?? throw new InvalidOperationException("Messaging configuration payload could not be deserialized.");
        }
    }
}
