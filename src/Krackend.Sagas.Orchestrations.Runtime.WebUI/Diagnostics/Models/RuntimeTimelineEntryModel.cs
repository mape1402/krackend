namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Represents a transition projected into a visual runtime storyline.
/// </summary>
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
