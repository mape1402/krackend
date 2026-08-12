namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a task in a stage, including execution policies and task-specific configuration.
/// </summary>
public sealed class TaskDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets stage definition id.
    /// </summary>
    public Id StageDefinitionId { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the notes associated with the entity.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets kind.
    /// </summary>
    public TaskKind Kind { get; set; }

    /// <summary>
    /// Gets or sets execution mode.
    /// </summary>
    public TaskExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Gets or sets parallel group id.
    /// </summary>
    public Id? ParallelGroupId { get; set; }

    /// <summary>
    /// Gets or sets execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; }

    /// <summary>
    /// Gets or sets transformation.
    /// </summary>
    public TransformationDefinition Transformation { get; set; }

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
    /// Gets or sets on error policy.
    /// </summary>
    public OnErrorPolicy OnErrorPolicy { get; set; } = OnErrorPolicy.Stop;

    /// <summary>
    /// Gets or sets rollback definition executed only if this task completed before compensation starts.
    /// </summary>
    public CompensationDefinition CompensationDefinition { get; set; }

    /// <summary>
    /// Gets or sets Dispatch type.
    /// </summary>
    public TaskDispatchType DispatchType { get; set; }

    /// <summary>
    /// Gets or sets is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
