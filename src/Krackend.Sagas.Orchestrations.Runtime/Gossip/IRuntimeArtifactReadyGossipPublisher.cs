namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Publishes artifact-ready gossip notifications to runtime replicas.
/// </summary>
public interface IRuntimeArtifactReadyGossipPublisher
{
    /// <summary>
    /// Publishes an artifact-ready gossip message.
    /// </summary>
    /// <param name="message">Gossip message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default);
}
