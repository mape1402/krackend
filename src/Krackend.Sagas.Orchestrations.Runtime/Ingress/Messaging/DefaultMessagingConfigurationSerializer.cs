using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    /// <summary>
    /// Serializes messaging ingress configuration payloads.
    /// </summary>
    public sealed class DefaultMessagingConfigurationSerializer : IMessagingConfigurationSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <inheritdoc />
        public string Serialize(MessagingConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return JsonSerializer.Serialize(new MessagingIngressSettings
            {
                Topic = configuration.Topic,
                Version = configuration.Version
            }, SerializerOptions);
        }

        /// <inheritdoc />
        public MessagingConfiguration Deserialize(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new ArgumentException("Messaging configuration payload cannot be empty.", nameof(rawJson));
            }

            var settings = JsonSerializer.Deserialize<MessagingIngressSettings>(rawJson, SerializerOptions)
                ?? throw new InvalidOperationException("Messaging configuration payload could not be deserialized.");

            return new MessagingConfiguration
            {
                Topic = settings.Topic,
                Version = settings.Version
            };
        }
    }
}
