namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    public interface IMessagingCommandSerializer
    {
        MessagingCommand Deserialize(string rawJson);
    }
}
