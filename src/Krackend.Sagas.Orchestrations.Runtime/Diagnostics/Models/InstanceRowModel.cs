namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents a compact orchestration instance row for the runtime dashboard grid.
/// </summary>
public sealed record InstanceRowModel(
    string Id,
    string OrchestrationDefinitionKey,
    string OrchestrationVersion,
    string CorrelationId,
    string SagaId,
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
    int RetryCount = 0)
{
    /// <summary>
    /// Gets the number of enabled orchestration tasks that are already complete
    /// for the instance. This is a diagnostics-only, display-safe progress value.
    /// </summary>
    public int CompletedTaskCount { get; init; }

    /// <summary>
    /// Gets the number of enabled orchestration tasks defined by the deployed
    /// artifact. Disabled design elements are intentionally excluded.
    /// </summary>
    public int TotalTaskCount { get; init; }

    /// <summary>
    /// Gets the most recently executed stage. This is distinct from the
    /// current stage, which is blank after terminal completion.
    /// </summary>
    public string LastExecutedStageKey { get; init; }

    /// <summary>
    /// Gets the most recently executed task. This is distinct from the
    /// current task, which is blank after terminal completion.
    /// </summary>
    public string LastExecutedTaskKey { get; init; }

    /// <summary>
    /// Gets the orchestration label including definition key and version when available.
    /// </summary>
    public string OrchestrationLabel
        => string.IsNullOrWhiteSpace(OrchestrationVersion)
            ? OrchestrationDefinitionKey
            : $"{OrchestrationDefinitionKey} v{OrchestrationVersion}";
}
