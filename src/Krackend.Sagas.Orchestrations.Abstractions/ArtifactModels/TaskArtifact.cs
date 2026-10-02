namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable task contract inside a stage artifact.
/// </summary>
public sealed record TaskArtifact(
    Id Id,
    string Key,
    string Name,
    int Order,
    string Notes,
    TaskKind Kind,
    TaskExecutionMode ExecutionMode,
    Id? ParallelGroupId,
    ExecutionConditionArtifact ExecutionCondition,
    TransformationArtifact Transformation,
    ITaskConfigurationArtifact Configuration,
    RetryPolicyArtifact RetryPolicy,
    TimeoutPolicyArtifact TimeoutPolicy,
    OnErrorPolicy OnErrorPolicy,
    CompensationArtifact Compensation,
    TaskDispatchType DispatchType,
    bool IsEnabled)
{
    /// <summary>
    /// Gets the extension key that owns this task capability. Empty means the Krackend built-in extension.
    /// </summary>
    public string ExtensionKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the capability key that executes this task.
    /// </summary>
    public string CapabilityKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the capability version selected for this task.
    /// </summary>
    public string CapabilityVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets task-level execution policy defaults and constraints.
    /// </summary>
    public ExecutionPolicyArtifact ExecutionPolicy { get; init; } = ExecutionPolicyArtifact.Empty;

    /// <summary>
    /// Gets runtime requirements declared by the selected capability.
    /// </summary>
    public ExecutionRuntimeRequirementsArtifact RuntimeRequirements { get; init; } =
        ExecutionRuntimeRequirementsArtifact.Empty;
}
