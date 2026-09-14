namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Represents a configured or executed stage in an orchestration trace.
/// </summary>
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
    int ParallelGroupCount = 0,
    bool HasExecution = true,
    string ConfiguredName = null,
    string ConfiguredDescription = null);
