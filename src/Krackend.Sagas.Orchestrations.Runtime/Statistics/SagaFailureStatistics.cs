namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Failure transition types that have an unambiguous operational meaning.</summary>
public static class SagaFailureStatistics
{
    /// <summary>Types included in the failure-event projection.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Categories = new Dictionary<string, string[]>
    {
        ["Timeout"] = ["TaskTimedOut"],
        ["Callback failure"] = ["TaskCallbackFailed"],
        ["Conditions and branching"] = ["TaskConditionFailed", "StageConditionFailed", "BranchEvaluationFailed"],
        ["Dispatch and retry"] = ["TaskDispatchConfigurationFailed", "TaskExecutionPolicyFailed", "TaskDispatchPreparationFailed",
            "TaskRetryPreparationFailed", "TaskDispatchQueueFailed", "TaskRetryQueueFailed", "TaskRetryConfigurationFailed", "TaskDispatchRecoveryFailed"],
        ["Compensation"] = ["CompensationFailed", "CompensationConditionFailed", "CompensationTransformationFailed"]
    };

    /// <summary>Transition types included in the projection.</summary>
    public static readonly string[] TransitionTypes = Categories.Values.SelectMany(x => x).ToArray();

    /// <summary>Whether a transition should be recorded as a failure event.</summary>
    public static bool IsFailure(string transitionType) => Array.IndexOf(TransitionTypes, transitionType) >= 0;
}

/// <summary>A count of recorded failure events in one operational category.</summary>
public sealed record SagaFailureCount(string Category, long Count);

/// <summary>Failure counts and whether historic transitions have been fully projected.</summary>
public sealed record SagaFailureReport(IReadOnlyCollection<SagaFailureCount> Items, bool HistoryComplete);

/// <summary>Reads the independent failure-event projection.</summary>
public interface ISagaFailureStatisticsReader
{
    /// <summary>Counts failure events in a UTC interval.</summary>
    Task<SagaFailureReport> GetFailuresAsync(DateTime fromUtc, DateTime beforeUtc, CancellationToken cancellationToken = default);
}

/// <summary>Projects a bounded batch of historical failure transitions.</summary>
public interface ISagaFailureStatisticsBackfill
{
    /// <summary>Processes one historical page, returning true after the initial history is complete.</summary>
    Task<bool> ProcessAsync(int batchSize, CancellationToken cancellationToken = default);
}
