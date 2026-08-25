namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Ignores artifact-ready gossip notifications when no gossip adapter is configured.
/// </summary>
internal sealed class NoopRuntimeArtifactReadyGossipPublisher : IRuntimeArtifactReadyGossipPublisher
{
    /// <inheritdoc />
    public Task PublishAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
