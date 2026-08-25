using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Schedules local ingress standup after a runtime artifact becomes ready.
/// </summary>
internal sealed class RuntimeArtifactReadyGossipHandler : IRuntimeArtifactReadyGossipHandler
{
    private readonly IRuntimeIngressStandupScheduler _standupScheduler;
    private readonly RuntimeOptions _runtimeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeArtifactReadyGossipHandler"/> class.
    /// </summary>
    public RuntimeArtifactReadyGossipHandler(
        IRuntimeIngressStandupScheduler standupScheduler,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _standupScheduler = standupScheduler ?? throw new ArgumentNullException(nameof(standupScheduler));
        _runtimeOptions = runtimeOptions?.Value ?? throw new ArgumentNullException(nameof(runtimeOptions));
    }

    /// <inheritdoc />
    public Task HandleAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!string.Equals(
            message.EnvironmentKey,
            _runtimeOptions.EnvironmentKey,
            StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        return _standupScheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
        {
            ArtifactId = message.ArtifactId,
            IngressGeneration = message.IngressGeneration,
            Reason = "gossip",
            RequestedOnUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}
