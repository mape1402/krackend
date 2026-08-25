namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Handles artifact-ready gossip notifications in one runtime replica.
/// </summary>
public interface IRuntimeArtifactReadyGossipHandler
{
    /// <summary>
    /// Handles an artifact-ready gossip message.
    /// </summary>
    /// <param name="message">Gossip message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default);
}
