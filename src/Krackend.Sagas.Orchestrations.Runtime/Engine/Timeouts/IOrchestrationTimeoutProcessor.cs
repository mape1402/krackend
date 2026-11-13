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

/// <summary>Optional capability for sharing artifact lookups within a timeout repair pass.</summary>
public interface IOrchestrationTimeoutBatchProcessor
{
    /// <summary>Processes one timeout using the supplied pass-local cache.</summary>
    Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id taskExecutionId, DateTime utcNow, OrchestrationTimeoutBatchContext batch,
        CancellationToken cancellationToken = default);
}

/// <summary>Resumes a timeout whose state was committed before its continuation ran.</summary>
public interface IOrchestrationTimeoutContinuationProcessor
{
    /// <summary>Re-evaluates only the matching current timed-out attempt and dispatch.</summary>
    Task ResumeTimedOutAsync(Id instanceId, Id taskId, Id attemptId, Id dispatchId, int attemptNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of processing a single response timeout action.</summary>
public sealed record TaskResponseTimeoutProcessingResult(
    bool Applied,
    DateTime? RescheduleOnUtc = null,
    bool RetryRequired = false)
{
    /// <summary>Gets a result for a stale or already-resolved timeout action.</summary>
    public static TaskResponseTimeoutProcessingResult Ignored { get; } = new(false);

    /// <summary>Gets whether a matching committed timeout may need its continuation resumed.</summary>
    public bool ResumeRequired { get; init; }

    /// <summary>Gets whether an on-time accepted response is still awaiting processing.</summary>
    public bool WaitingForResponseProcessing { get; init; }
}
