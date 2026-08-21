namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Represents a persisted orchestration transition.
/// </summary>
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
