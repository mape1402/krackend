namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    public interface IMessagingConfigurationSerializer 
    {
        MessagingConfiguration Deserialize(string rawJson);
    }
}
