namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    /// <summary>
    /// Serializes and deserializes messaging ingress configuration payloads.
    /// </summary>
    public interface IMessagingConfigurationSerializer
    {
        /// <summary>
        /// Serializes a messaging ingress configuration.
        /// </summary>
        /// <param name="configuration">Messaging configuration.</param>
        /// <returns>Serialized settings payload.</returns>
        string Serialize(MessagingConfiguration configuration);

        /// <summary>
        /// Deserializes a messaging ingress configuration.
        /// </summary>
        /// <param name="rawJson">Serialized settings payload.</param>
        /// <returns>Messaging configuration.</returns>
        MessagingConfiguration Deserialize(string rawJson);
    }
}
