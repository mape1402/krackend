using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Default artifact-ready notifier that always schedules local standup and optionally publishes gossip.
/// </summary>
internal sealed class RuntimeArtifactReadyNotifier : IRuntimeArtifactReadyNotifier
{
    private readonly IRuntimeArtifactReadyGossipHandler _localHandler;
    private readonly IRuntimeArtifactReadyGossipPublisher _gossipPublisher;
    private readonly RuntimeGossipOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactReadyNotifier"/> class.
    /// </summary>
    public RuntimeArtifactReadyNotifier(
        IRuntimeArtifactReadyGossipHandler localHandler,
        IRuntimeArtifactReadyGossipPublisher gossipPublisher,
        IOptions<RuntimeGossipOptions> options)
    {
        _localHandler = localHandler ?? throw new ArgumentNullException(nameof(localHandler));
        _gossipPublisher = gossipPublisher ?? throw new ArgumentNullException(nameof(gossipPublisher));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task NotifyReadyAsync(
        RuntimeArtifactReadyGossipMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _localHandler.HandleAsync(message, cancellationToken);
        if (_options.Enabled)
        {
            await _gossipPublisher.PublishAsync(message, cancellationToken);
        }
    }
}
