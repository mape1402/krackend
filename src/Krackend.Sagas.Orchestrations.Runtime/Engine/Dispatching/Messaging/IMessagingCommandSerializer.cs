namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    public interface IMessagingCommandSerializer
    {
        string Serialize(MessagingCommand command);

        MessagingCommand Deserialize(string rawJson);
    }
}
