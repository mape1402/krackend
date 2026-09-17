namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Represents the input required to evaluate an orchestration condition.
/// </summary>
public sealed record OrchestrationConditionEvaluationRequest
{
    /// <summary>
    /// Gets the condition artifact to evaluate.
    /// </summary>
    public ExecutionConditionArtifact Condition { get; init; }

    /// <summary>
    /// Gets the runtime payload context available to the condition.
    /// </summary>
    public OrchestrationPayloadContext PayloadContext { get; init; }

    /// <summary>
    /// Gets the key of the element that owns the condition.
    /// </summary>
    public string ElementKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the condition phase, such as Stage, Task, Branch, or Compensation.
    /// </summary>
    public string Phase { get; init; } = string.Empty;
}
