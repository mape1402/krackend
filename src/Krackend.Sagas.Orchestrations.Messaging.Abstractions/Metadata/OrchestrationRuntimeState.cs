namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Snapshot of the orchestration position when a runtime message is published or consumed.
/// </summary>
public sealed class OrchestrationRuntimeState
{
    /// <summary>
    /// Gets the current orchestration or task status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the current stage key. Orchestrator uses stages, not steps.
    /// </summary>
    public required string CurrentStageKey { get; init; }

    /// <summary>
    /// Gets the current task key inside the current stage.
    /// </summary>
    public required string CurrentTaskKey { get; init; }

    /// <summary>
    /// Gets the current task attempt number.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the current execution segment started.
    /// </summary>
    public DateTime StartedOnUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this state snapshot was produced.
    /// </summary>
    public DateTime UpdatedOnUtc { get; init; }
}
