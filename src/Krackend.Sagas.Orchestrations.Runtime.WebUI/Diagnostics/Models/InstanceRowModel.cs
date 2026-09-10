namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

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
    /// Gets the orchestration label including definition key and version when available.
    /// </summary>
    public string OrchestrationLabel
        => string.IsNullOrWhiteSpace(OrchestrationVersion)
            ? OrchestrationDefinitionKey
            : $"{OrchestrationDefinitionKey} v{OrchestrationVersion}";
}
