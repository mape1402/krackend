using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;

/// <summary>
/// Represents TaskDefinitionEntity.
/// </summary>
public sealed class TaskDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets StageDefinitionId.
    /// </summary>
    public Id StageDefinitionId { get; set; }
    /// <summary>
    /// Gets or sets Key.
    /// </summary>
    public string Key { get; set; }
    /// <summary>
    /// Gets or sets Name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets Order.
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Gets or sets Kind.
    /// </summary>
    public TaskKind Kind { get; set; }
    /// <summary>
    /// Gets or sets ExecutionMode.
    /// </summary>
    public TaskExecutionMode ExecutionMode { get; set; }
    /// <summary>
    /// Gets or sets ParallelGroupId.
    /// </summary>
    public Id? ParallelGroupId { get; set; }
    /// <summary>
    /// Gets or sets OnErrorPolicy.
    /// </summary>
    public OnErrorPolicy OnErrorPolicy { get; set; }
    /// <summary>
    /// Gets or sets DispatchType.
    /// </summary>
    public TaskDispatchType DispatchType { get; set; }
    /// <summary>
    /// Gets or sets IsEnabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// Gets or sets Notes.
    /// </summary>
    public string Notes { get; set; }
    /// <summary>
    /// Gets or sets ExecutionCondition.
    /// </summary>
    public ExecutionConditionJsonModel ExecutionCondition { get; set; }
    /// <summary>
    /// Gets or sets Transformation.
    /// </summary>
    public TransformationDefinitionJsonModel Transformation { get; set; }
    /// <summary>
    /// Gets or sets Configuration.
    /// </summary>
    public TaskConfigurationEnvelopeJsonModel Configuration { get; set; }
    /// <summary>
    /// Gets or sets RetryPolicy.
    /// </summary>
    public RetryPolicyJsonModel RetryPolicy { get; set; }
    /// <summary>
    /// Gets or sets TimeoutPolicy.
    /// </summary>
    public TimeoutPolicyJsonModel TimeoutPolicy { get; set; }
    /// <summary>
    /// Gets or sets CompensationDefinition.
    /// </summary>
    public CompensationDefinitionJsonModel CompensationDefinition { get; set; }
}
