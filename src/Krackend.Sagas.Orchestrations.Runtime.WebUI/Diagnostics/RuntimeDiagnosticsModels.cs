namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

public sealed record InstanceRowModel(
    string Id,
    string OrchestrationDefinitionKey,
    string CorrelationId,
    string ExecutionKey,
    string Status,
    string StatusClass,
    string CurrentStageKey,
    string CurrentTaskKey,
    DateTime StartedOnUtc,
    DateTime LastUpdatedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string ErrorSummary,
    string CurrentParallelGroupKey = null,
    string FinalOutcome = null,
    DateTime? StoppedOnUtc = null,
    DateTime? CompensationStartedOnUtc = null,
    DateTime? CompensatedOnUtc = null,
    int RetryCount = 0);

public sealed record TrafficPointModel(
    DateTime BucketUtc,
    int Started,
    int Completed,
    int Failed);

public sealed record RuntimeSummaryModel(
    int Active,
    int Waiting,
    int CompletedLastMinute,
    int FailedLastMinute,
    int CompletedLastHour,
    int FailedLastHour,
    DateTime MinuteSinceUtc,
    DateTime HourSinceUtc);

public sealed record RuntimeDashboardSnapshotModel(
    RuntimeSummaryModel Summary,
    IReadOnlyCollection<InstanceRowModel> Instances,
    IReadOnlyCollection<TrafficPointModel> Traffic);

public sealed record RuntimeDashboardSummaryModel(
    RuntimeSummaryModel Summary,
    IReadOnlyCollection<TrafficPointModel> Traffic);

public sealed record InstanceDetailModel(
    InstanceRowModel Instance,
    IReadOnlyCollection<StageDetailModel> Stages,
    IReadOnlyCollection<TaskDetailModel> Tasks,
    IReadOnlyCollection<TransitionDetailModel> Transitions,
    string SnapshotPayload,
    string Metadata,
    IReadOnlyCollection<CompensationDetailModel> Compensations,
    IReadOnlyCollection<RuntimeTimelineEntryModel> FunctionalTimeline,
    IReadOnlyCollection<RuntimeTimelineEntryModel> TechnicalTimeline);

public sealed record StageDetailModel(
    string Id,
    string StageKey,
    int Order,
    string Status,
    string StatusClass,
    DateTime? StartedOnUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string ErrorSummary,
    string Metadata,
    IReadOnlyCollection<TaskDetailModel> Tasks,
    bool WasSkipped = false,
    string SkipReason = null,
    bool? ExecutionConditionResult = null,
    int ParallelGroupCount = 0);

public sealed record TaskDetailModel(
    string Id,
    string StageExecutionId,
    string TaskKey,
    string TaskKind,
    string ExecutionMode,
    string Status,
    bool AwaitResponse,
    string CorrelationId,
    DateTime? StartedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    DateTime? TimedOutOnUtc,
    int LastAttemptNumber,
    string OutputVariablesPayload,
    string Metadata,
    IReadOnlyCollection<TaskAttemptDetailModel> Attempts,
    string SagaId = null,
    string OnErrorPolicy = null,
    string ParallelGroupId = null,
    bool WasSkipped = false,
    string SkipReason = null,
    bool? ExecutionConditionResult = null);

public sealed record TaskAttemptDetailModel(
    string Id,
    int AttemptNumber,
    string Status,
    DateTime? StartedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    DateTime? TimedOutOnUtc,
    string RequestPayload,
    string ResponsePayload,
    string ErrorCode,
    string ErrorMessage,
    DispatchDetailModel Dispatch,
    string Metadata,
    string TaskExecutionId = null,
    string DispatchId = null);

public sealed record DispatchDetailModel(
    string Id,
    string DispatchType,
    string Destination,
    string DispatchStatus,
    string CommandId,
    string CorrelationId,
    DateTime? SentOnUtc,
    DateTime? AcknowledgedOnUtc,
    DateTime? FailedOnUtc,
    string FailureReason,
    string RequestPayload,
    string Metadata,
    string TaskExecutionAttemptId = null);

public sealed record CompensationDetailModel(
    string Id,
    string SourceTaskExecutionId,
    string SourceTaskKey,
    string CompensationTaskKey,
    string Status,
    DateTime? StartedOnUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string RequestPayload,
    string ResponsePayload,
    string ErrorMessage,
    string Metadata);

public sealed record TransitionDetailModel(
    string Id,
    string TransitionType,
    string FromStatus,
    string ToStatus,
    DateTime OccurredOnUtc,
    string StageExecutionId,
    string TaskExecutionId,
    string TaskExecutionAttemptId,
    string Message,
    string ProducedBy,
    string Payload,
    string StageKey = null,
    string TaskKey = null);

public sealed record RuntimeTimelineEntryModel(
    string Id,
    string TimelineKind,
    string TransitionType,
    string FromStatus,
    string ToStatus,
    DateTime OccurredOnUtc,
    string StageExecutionId,
    string StageKey,
    string TaskExecutionId,
    string TaskKey,
    string TaskExecutionAttemptId,
    string Message,
    string ProducedBy,
    string Payload);
