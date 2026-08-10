using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents instance variable in the orchestrator domain.
/// </summary>
public sealed class InstanceVariable
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration instance id.
    /// </summary>
    public Id OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public VariableScope Scope { get; set; }

    /// <summary>
    /// Gets or sets value type.
    /// </summary>
    public VariableValueType ValueType { get; set; }

    /// <summary>
    /// Gets or sets value.
    /// </summary>
    public JsonNode Value { get; set; }

    /// <summary>
    /// Gets or sets is sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets source type.
    /// </summary>
    public required string SourceType { get; set; }

    /// <summary>
    /// Gets or sets source reference.
    /// </summary>
    public string SourceReference { get; set; }

    /// <summary>
    /// Gets or sets created on utc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated on utc.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets last updated by.
    /// </summary>
    public string LastUpdatedBy { get; set; }
}
