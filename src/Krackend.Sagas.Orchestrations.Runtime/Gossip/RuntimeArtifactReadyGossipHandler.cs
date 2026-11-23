using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Schedules local ingress standup after a runtime artifact becomes ready.
/// </summary>
internal sealed class RuntimeArtifactReadyGossipHandler : IRuntimeArtifactReadyGossipHandler
{
    private readonly IRuntimeIngressStandupScheduler _standupScheduler;
    private readonly RuntimeArtifactSnapshotCache? _artifactCache;
    private readonly RuntimeBackchannelReplyAddressCache? _replyAddressCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactReadyGossipHandler"/> class.
    /// </summary>
    public RuntimeArtifactReadyGossipHandler(
        IRuntimeIngressStandupScheduler standupScheduler,
        RuntimeArtifactSnapshotCache? artifactCache = null,
        RuntimeBackchannelReplyAddressCache? replyAddressCache = null)
    {
        _standupScheduler = standupScheduler ?? throw new ArgumentNullException(nameof(standupScheduler));
        _artifactCache = artifactCache;
        _replyAddressCache = replyAddressCache;
    }

    /// <inheritdoc />
    public Task HandleAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _artifactCache?.Invalidate(message.ArtifactId);
        _replyAddressCache?.Invalidate(message.ArtifactId);
        return _standupScheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
        {
            ArtifactId = message.ArtifactId,
            IngressGeneration = message.IngressGeneration,
            Reason = "gossip",
            RequestedOnUtc = DateTime.UtcNow,
            RefreshToken = message.RefreshToken
        }, cancellationToken);
    }
}
