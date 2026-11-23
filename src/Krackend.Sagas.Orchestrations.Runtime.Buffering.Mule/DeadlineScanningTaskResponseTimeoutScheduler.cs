using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Selects the persisted task deadline as the durable timeout source. The runtime
/// timeout processor reads only indexed, due waits and applies the normal timeout,
/// retry and compensation decisions under the owning instance coordinator.
/// </summary>
internal sealed class DeadlineScanningTaskResponseTimeoutScheduler : ITaskResponseTimeoutScheduler
{
    public bool UsesDurableScheduling => false;

    public Task<TaskResponseTimeoutScheduleResult> ScheduleAsync(
        TaskResponseTimeoutScheduleRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TaskResponseTimeoutScheduleResult(null, null));

    public Task CancelAsync(
        TaskResponseTimeoutCancellationRequest request,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
