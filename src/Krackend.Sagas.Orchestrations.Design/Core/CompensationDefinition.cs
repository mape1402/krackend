namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents compensation behavior executed when a task must be rolled back or recovered.
/// </summary>
public sealed class CompensationDefinition
{
    /// <summary>
    /// Gets or sets compensation task kind.
    /// </summary>
    public TaskKind CompensationTaskKind { get; set; }

    /// <summary>
    /// Gets or sets transformation.
    /// </summary>
    public TransformationDefinition Transformation { get; set; }

    /// <summary>
    /// Gets or sets execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; }

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
    /// Gets or sets Dispatch type.
    /// </summary>
    public TaskDispatchType DispatchType { get; set; }
}
