using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>
/// Schedules and cancels one durable timeout action for an awaited task response. Transport-specific implementations
/// own the durable mechanism; the orchestration engine only expresses the required deadline.
/// </summary>
public interface ITaskResponseTimeoutScheduler
{
    /// <summary>Gets whether response timeouts are handled by durable scheduled actions.</summary>
    bool UsesDurableScheduling { get; }

    /// <summary>Schedules the timeout for the supplied response wait.</summary>
    Task<TaskResponseTimeoutScheduleResult> ScheduleAsync(
        TaskResponseTimeoutScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the durable timeout action when a callback resolves the response wait.</summary>
    Task CancelAsync(TaskResponseTimeoutCancellationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs a bounded set of waits whose state committed but whose timeout action reference was not captured.
    /// Implementations must not scan healthy awaited tasks.
    /// </summary>
    Task<int> RepairAsync(int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

/// <summary>Identifies an awaited response and its exact deadline.</summary>
public sealed class TaskResponseTimeoutScheduleRequest
{
    /// <summary>Gets the orchestration instance that owns the awaited response.</summary>
    public required Id OrchestrationInstanceId { get; init; }

    /// <summary>Gets the task execution that is awaiting a response.</summary>
    public required Id TaskExecutionId { get; init; }

    /// <summary>Gets the specific task attempt that owns the awaited response.</summary>
    public required Id TaskExecutionAttemptId { get; init; }

    /// <summary>Gets the dispatch that was sent for the awaited response.</summary>
    public required Id DispatchId { get; init; }

    /// <summary>Gets the stable attempt number used for durable deduplication.</summary>
    public required int AttemptNumber { get; init; }

    /// <summary>Gets the UTC instant at which the timeout action becomes eligible.</summary>
    public required DateTime DueOnUtc { get; init; }
}

/// <summary>Identifies a scheduled timeout that should be cancelled after a callback.</summary>
public sealed class TaskResponseTimeoutCancellationRequest
{
    /// <summary>Gets the orchestration instance that owns the timeout action.</summary>
    public required Id OrchestrationInstanceId { get; init; }

    /// <summary>Gets the task execution that resolved before the timeout action ran.</summary>
    public required Id TaskExecutionId { get; init; }

    /// <summary>Gets the durable action identifier, when it was captured after scheduling.</summary>
    public string ActionId { get; init; }

    /// <summary>Gets the Mule lane used to schedule the timeout action.</summary>
    public string Lane { get; init; }

    /// <summary>
    /// Gets the attempt that owned the wait. It lets a durable adapter recover the action by its stable
    /// deduplication key when a very fast callback read the task before its action reference was captured.
    /// </summary>
    public Id? TaskExecutionAttemptId { get; init; }

    /// <summary>Gets the stable attempt number used in durable timeout deduplication.</summary>
    public int? AttemptNumber { get; init; }

    /// <summary>Gets the deadline used in durable timeout deduplication.</summary>
    public DateTime? DueOnUtc { get; init; }
}

/// <summary>Describes a durable timeout scheduling attempt.</summary>
public sealed record TaskResponseTimeoutScheduleResult(string ActionId, string Lane);

/// <summary>Default engine implementation for runtimes that do not register a durable scheduling adapter.</summary>
internal sealed class NoopTaskResponseTimeoutScheduler : ITaskResponseTimeoutScheduler
{
    public bool UsesDurableScheduling => false;

    public Task<TaskResponseTimeoutScheduleResult> ScheduleAsync(
        TaskResponseTimeoutScheduleRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TaskResponseTimeoutScheduleResult(null, null));

    public Task CancelAsync(TaskResponseTimeoutCancellationRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> RepairAsync(int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
