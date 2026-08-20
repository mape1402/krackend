namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    public interface IMessagingDispatchAdapter
    {
        Task PublishAsync(MessagingCommand command, CancellationToken cancellationToken = default);
    }
}
