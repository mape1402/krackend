using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents CompensationDefinitionJsonModel.
/// </summary>
public sealed class CompensationDefinitionJsonModel
{
    /// <summary>
    /// Gets or sets whether the rollback execution condition is enabled.
    /// </summary>
    public bool HasExecutionCondition { get; set; }
    /// <summary>
    /// Gets or sets whether the rollback transformation is enabled.
    /// </summary>
    public bool HasTransformation { get; set; }
    /// <summary>
    /// Gets or sets CompensationTaskKind.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskKind CompensationTaskKind { get; set; }
    /// <summary>
    /// Gets or sets Transformation.
    /// </summary>
    public TransformationDefinitionJsonModel Transformation { get; set; }
    /// <summary>
    /// Gets or sets ExecutionCondition.
    /// </summary>
    public ExecutionConditionJsonModel ExecutionCondition { get; set; }
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
    /// Gets or sets DispatchType.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskDispatchType DispatchType { get; set; }
}
