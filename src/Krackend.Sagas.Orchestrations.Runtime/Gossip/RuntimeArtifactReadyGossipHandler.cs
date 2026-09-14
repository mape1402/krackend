using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Schedules local ingress standup after a runtime artifact becomes ready.
/// </summary>
internal sealed class RuntimeArtifactReadyGossipHandler : IRuntimeArtifactReadyGossipHandler
{
    private readonly IRuntimeIngressStandupScheduler _standupScheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactReadyGossipHandler"/> class.
    /// </summary>
    public RuntimeArtifactReadyGossipHandler(IRuntimeIngressStandupScheduler standupScheduler)
    {
        _standupScheduler = standupScheduler ?? throw new ArgumentNullException(nameof(standupScheduler));
    }

    /// <inheritdoc />
    public Task HandleAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _standupScheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
        {
            ArtifactId = message.ArtifactId,
            IngressGeneration = message.IngressGeneration,
            Reason = "gossip",
            RequestedOnUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}
