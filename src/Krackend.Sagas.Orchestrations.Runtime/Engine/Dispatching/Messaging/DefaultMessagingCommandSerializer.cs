using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    internal sealed class DefaultMessagingCommandSerializer : IMessagingCommandSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public string Serialize(MessagingCommand command)
            => JsonSerializer.Serialize(command, SerializerOptions);

        public MessagingCommand Deserialize(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new ArgumentException("Messaging command payload cannot be empty.", nameof(rawJson));
            }

            return JsonSerializer.Deserialize<MessagingCommand>(rawJson, SerializerOptions)
                ?? throw new InvalidOperationException("Messaging command payload could not be deserialized.");
        }
    }
}
