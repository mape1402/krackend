using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>Durable Mule action that applies one task response timeout at its persisted deadline.</summary>
[MuleAction(MuleActionKeys.TaskResponseTimeoutAction)]
public sealed class TaskResponseTimeoutAction(
    IOrchestrationTimeoutProcessor processor,
    ITaskResponseTimeoutScheduler scheduler) : IMuleAction<TaskResponseTimeoutActionRequest>
{
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        MuleActionContext<TaskResponseTimeoutActionRequest> context,
        CancellationToken cancellationToken)
    {
        await MuleCooperativeExecution.RunAsync(context, () => ExecuteCoreAsync(context, cancellationToken));
    }

    private async Task ExecuteCoreAsync(MuleActionContext<TaskResponseTimeoutActionRequest> context, CancellationToken cancellationToken)
    {
        var request = context.Payload ?? throw new InvalidOperationException("Task response timeout payload cannot be empty.");
        var result = await processor.ProcessTaskTimeoutAsync(
            new Id(System.Ulid.Parse(request.OrchestrationInstanceId)),
            new Id(System.Ulid.Parse(request.TaskExecutionId)),
            DateTime.UtcNow,
            cancellationToken);

        // Let Mule retain and retry this action. Scheduling another action with the same
        // deadline would deduplicate against the action currently being executed.
        if (result.RetryRequired)
        {
            MuleCooperativeExecution.Defer(context, result.WaitingForResponseProcessing
                ? "krackend.response-accepted-pending" : "krackend.timeout-pending");
            return;
        }

        if (result.ResumeRequired && processor is IOrchestrationTimeoutContinuationProcessor continuation)
        {
            await continuation.ResumeTimedOutAsync(
                new Id(System.Ulid.Parse(request.OrchestrationInstanceId)),
                new Id(System.Ulid.Parse(request.TaskExecutionId)),
                new Id(System.Ulid.Parse(request.TaskExecutionAttemptId)),
                new Id(System.Ulid.Parse(request.DispatchId)),
                request.AttemptNumber,
                cancellationToken);
        }

        if (result.RescheduleOnUtc is not { } rescheduleOnUtc)
        {
            return;
        }

        MuleCooperativeExecution.Defer(context, "krackend.timeout-before-deadline",
            new DateTimeOffset(DateTime.SpecifyKind(rescheduleOnUtc, DateTimeKind.Utc)));
    }
}

/// <summary>Serializable durable payload for one task response timeout.</summary>
public sealed class TaskResponseTimeoutActionRequest
{
    /// <summary>Gets the owning orchestration instance identifier.</summary>
    public required string OrchestrationInstanceId { get; init; }

    /// <summary>Gets the awaited task execution identifier.</summary>
    public required string TaskExecutionId { get; init; }

    /// <summary>Gets the task attempt identifier used to schedule this timer.</summary>
    public required string TaskExecutionAttemptId { get; init; }

    /// <summary>Gets the outbound dispatch identifier used to schedule this timer.</summary>
    public required string DispatchId { get; init; }

    /// <summary>Gets the monotonic task attempt number used for durable deduplication.</summary>
    public required int AttemptNumber { get; init; }

    /// <summary>Gets the original UTC deadline of this timeout action.</summary>
    public required DateTime DueOnUtc { get; init; }
}
