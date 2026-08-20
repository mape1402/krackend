namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    public interface IMessagingConfigurationSerializer 
    {
        string Serialize(MessagingConfiguration configuration);

        MessagingConfiguration Deserialize(string rawJson);
    }
}
