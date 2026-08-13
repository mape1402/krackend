namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents rollback behavior for a task that completed before the orchestration entered compensation.
/// </summary>
public sealed class CompensationDefinition
{
    /// <summary>
    /// Gets or sets rollback task kind.
    /// </summary>
    public TaskKind CompensationTaskKind { get; set; }

    /// <summary>
    /// Gets or sets transformation.
    /// </summary>
    public TransformationDefinition Transformation { get; set; }

    /// <summary>
    /// Gets or sets whether transformation is enabled.
    /// </summary>
    public bool HasTransformation { get; set; }

    /// <summary>
    /// Gets or sets execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; }

    /// <summary>
    /// Gets or sets whether execution condition is enabled.
    /// </summary>
    public bool HasExecutionCondition { get; set; }

    /// <summary>
    /// Gets or sets configuration.
    /// </summary>
    public ITaskConfiguration Configuration { get; set; }

    /// <summary>
    /// Gets or sets retry policy.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; }

    /// <summary>
    /// Gets or sets timeout policy.
    /// </summary>
    public TimeoutPolicy TimeoutPolicy { get; set; }

    /// <summary>
    /// Gets or sets dispatch type.
    /// </summary>
    public TaskDispatchType DispatchType { get; set; }
}
