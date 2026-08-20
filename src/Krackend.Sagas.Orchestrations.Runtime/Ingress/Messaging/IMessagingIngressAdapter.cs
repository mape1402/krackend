namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    public interface IMessagingIngressAdapter
    {
        Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
