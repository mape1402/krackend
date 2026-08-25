namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Notifies the local replica and optional peer replicas that an artifact is ready for standup.
/// </summary>
public interface IRuntimeArtifactReadyNotifier
{
    /// <summary>
    /// Notifies runtime replicas that an artifact is ready.
    /// </summary>
    /// <param name="message">Artifact-ready message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyReadyAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default);
}
