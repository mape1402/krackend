namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents a configured or executed task inside a runtime stage.
/// </summary>
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
    bool? ExecutionConditionResult = null,
    bool HasExecution = true,
    string ConfiguredName = null,
    string ConfiguredNotes = null,
    int Order = 0,
    string ErrorSummary = null);
