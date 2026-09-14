namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Schedules local ingress standup work for a runtime replica.
/// </summary>
public interface IRuntimeIngressStandupScheduler
{
    /// <summary>
    /// Schedules ingress standup for one runtime artifact.
    /// </summary>
    /// <param name="request">Standup request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ScheduleStandupAsync(RuntimeIngressStandupRequest request, CancellationToken cancellationToken = default);
}
