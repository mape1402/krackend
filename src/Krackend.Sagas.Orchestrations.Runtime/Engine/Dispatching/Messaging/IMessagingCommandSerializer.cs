namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    /// <summary>
    /// Serializes and deserializes messaging commands used by the runtime dispatcher.
    /// </summary>
    public interface IMessagingCommandSerializer
    {
        /// <summary>
        /// Serializes a messaging command to its wire representation.
        /// </summary>
        /// <param name="command">Messaging command to serialize.</param>
        /// <returns>The serialized command payload.</returns>
        string Serialize(MessagingCommand command);

        /// <summary>
        /// Deserializes a messaging command from its wire representation.
        /// </summary>
        /// <param name="rawJson">Serialized command payload.</param>
        /// <returns>The deserialized messaging command.</returns>
        MessagingCommand Deserialize(string rawJson);
    }
}
