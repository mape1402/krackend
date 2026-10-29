using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>
/// Processes orchestration task executions whose configured response timeout has elapsed.
/// </summary>
public interface IOrchestrationTimeoutProcessor
{
    /// <summary>
    /// Applies timeout policies for waiting task executions due at the supplied UTC instant.
    /// </summary>
    /// <param name="utcNow">Current UTC instant used to evaluate due timeouts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of task executions whose timeout policy was applied.</returns>
    Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the timeout policy for one durable scheduled response wait. The method re-reads state under the
    /// orchestration lease, so a timer that races a callback safely becomes a no-op.
    /// </summary>
    Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id taskExecutionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => Task.FromResult(TaskResponseTimeoutProcessingResult.Ignored);
}

/// <summary>Result of processing a single response timeout action.</summary>
public sealed record TaskResponseTimeoutProcessingResult(
    bool Applied,
    DateTime? RescheduleOnUtc = null)
{
    /// <summary>Gets a result for a stale or already-resolved timeout action.</summary>
    public static TaskResponseTimeoutProcessingResult Ignored { get; } = new(false);
}
