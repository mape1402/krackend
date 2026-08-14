namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>
/// Canonical reactive event names emitted by the orchestration runtime.
/// </summary>
public static class RuntimeReactiveEventNames
{
    public const string OrchestrationStarted = "orchestration.started";
    public const string OrchestrationWaiting = "orchestration.waiting";
    public const string OrchestrationCompensating = "orchestration.compensating";
    public const string OrchestrationCompleted = "orchestration.completed";
    public const string OrchestrationFailed = "orchestration.failed";
    public const string StageStarted = "stage.started";
    public const string StageCompleted = "stage.completed";
    public const string StageFailed = "stage.failed";
    public const string StageSkipped = "stage.skipped";
    public const string TaskStarted = "task.started";
    public const string TaskInputTransformed = "task.input.transformed";
    public const string TaskCompleted = "task.completed";
    public const string TaskFailed = "task.failed";
    public const string TaskSkipped = "task.skipped";
    public const string TaskRetryScheduled = "task.retry.scheduled";
    public const string TaskRetryStarted = "task.retry.started";
    public const string TaskTimedOut = "task.timed.out";
    public const string TaskTimeoutPolicyApplied = "task.timeout.policy.applied";
    public const string TaskReconciliationUnsupported = "task.reconciliation.unsupported";
    public const string TaskErrorPolicyApplied = "task.error.policy.applied";
    public const string TaskWaiting = "task.waiting";
    public const string TaskResponseReceived = "task.response.received";
    public const string DispatchPublished = "dispatch.published";
    public const string DispatchFailed = "dispatch.failed";
    public const string CompensationScheduled = "compensation.scheduled";
    public const string CompensationStarted = "compensation.started";
    public const string CompensationDispatched = "compensation.dispatched";
    public const string CompensationCompleted = "compensation.completed";
    public const string CompensationFailed = "compensation.failed";
    public const string OrchestrationCompensated = "orchestration.compensated";
    public const string BranchEvaluated = "branch.evaluated";
    public const string BranchTaken = "branch.taken";
    public const string BranchNotTaken = "branch.not.taken";
    public const string BranchUnsupported = "branch.unsupported";
    public const string ParallelGroupStarted = "parallel.group.started";
    public const string ParallelGroupCompleted = "parallel.group.completed";
    public const string ParallelGroupFailed = "parallel.group.failed";
    public const string TransitionRecorded = "transition.recorded";
}
