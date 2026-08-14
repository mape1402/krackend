namespace Krackend.Sagas.Orchestrations.Engine;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;

/// <summary>
/// Schedules durable runtime work without exposing the underlying durable action engine to transport adapters.
/// </summary>
public interface IRuntimeDurableWorkScheduler
{
    /// <summary>
    /// Schedules a canonical ingress envelope for durable processing.
    /// </summary>
    ValueTask<Guid> ScheduleProcessIngress(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a canonical dispatch envelope for durable publishing.
    /// </summary>
    ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default);
}
