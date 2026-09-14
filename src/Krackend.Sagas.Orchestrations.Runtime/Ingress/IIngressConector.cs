namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Connects and disconnects runtime ingress endpoints for one transport.
/// </summary>
public interface IIngressConector
{
    /// <summary>
    /// Connects an ingress configuration in the current runtime process.
    /// </summary>
    /// <param name="configuration">Ingress configuration to connect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(IngressConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects an ingress configuration from the current runtime process.
    /// </summary>
    /// <param name="connectorId">Ingress connector id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(string connectorId, CancellationToken cancellationToken = default);
}
