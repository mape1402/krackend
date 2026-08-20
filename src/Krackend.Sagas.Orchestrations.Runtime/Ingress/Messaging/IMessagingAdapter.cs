namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    public interface IMessagingAdapter
    {
        Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
