namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Schedules durable runtime artifact projection work.
/// </summary>
public interface IRuntimeArtifactProjectionScheduler
{
    /// <summary>
    /// Schedules ingress configuration projection for an accepted artifact.
    /// </summary>
    /// <param name="request">Projection request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ScheduleProjectionAsync(
        RuntimeArtifactProjectionRequest request,
        CancellationToken cancellationToken = default);
}
