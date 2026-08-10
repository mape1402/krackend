using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for task definition.
/// </summary>
public sealed class TaskDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the stage definition id.
    /// </summary>
    public string StageDefinitionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public TaskKind Kind { get; set; }
    /// <summary>
    /// Gets or sets the execution mode.
    /// </summary>
    public TaskExecutionMode ExecutionMode { get; set; }
    /// <summary>
    /// Gets or sets the parallel group id.
    /// </summary>
    public string ParallelGroupId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the execution condition.
    /// </summary>
    public ExecutionCondition ExecutionCondition { get; set; } = null!;
    /// <summary>
    /// Gets or sets the transformation.
    /// </summary>
    public TransformationDefinition Transformation { get; set; } = null!;
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    public ITaskConfiguration Configuration { get; set; } = null!;
    /// <summary>
    /// Gets or sets the retry policy.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = null!;
    /// <summary>
    /// Gets or sets the timeout policy.
    /// </summary>
    public TimeoutPolicy TimeoutPolicy { get; set; } = null!;
    /// <summary>
    /// Gets or sets the on error policy.
    /// </summary>
    public OnErrorPolicy OnErrorPolicy { get; set; }
    /// <summary>
    /// Gets or sets the compensation definition.
    /// </summary>
    public CompensationDefinition CompensationDefinition { get; set; } = null!;
    /// <summary>
    /// Gets or sets the dispatch type.
    /// </summary>
    public TaskDispatchType DispatchType { get; set; }
    /// <summary>
    /// Gets or sets the is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

