namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;

/// <summary>
/// Connects messaging ingress configurations through the configured messaging provider.
/// </summary>
public interface IMessagingIngressAdapter
{
    /// <summary>
    /// Connects a messaging ingress endpoint in the current runtime process.
    /// </summary>
    /// <param name="configuration">Messaging ingress configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(MessagingConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects a messaging ingress endpoint from the current runtime process.
    /// </summary>
    /// <param name="connectorId">Ingress connector id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default);
}
