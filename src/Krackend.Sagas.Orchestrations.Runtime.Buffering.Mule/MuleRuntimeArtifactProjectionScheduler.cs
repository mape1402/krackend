using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Mule;
using Mule.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Schedules artifact projection through the Mule provider selected by the host.
/// </summary>
/// <remarks>
/// Runtime storage and Mule storage are allowed to use different providers. Writing a
/// provider-specific DurableAction row through the runtime DbContext leaves projection
/// permanently pending when Mule itself uses Redis (or another store). Enqueueing through
/// <see cref="IMuleClient"/> keeps the action, wake-up and recovery state in one provider.
/// Projection is idempotent by artifact generation, so an action that races the artifact
/// commit simply retries until the committed artifact becomes visible.
/// </remarks>
internal sealed class MuleRuntimeArtifactProjectionScheduler(IMuleClient client)
    : IRuntimeDurableRolloutProjectionScheduler
{
    public async Task ScheduleProjectionAsync(
        RuntimeArtifactProjectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await client.EnqueueAsync(
            ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations),
            request,
            options =>
            {
                options.Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane;
                options.CorrelationId = request.ArtifactId;
                options.DeduplicationKey = $"{request.ArtifactId}:{request.IngressGeneration}";
            },
            cancellationToken);
    }
}
