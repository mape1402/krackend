using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents VariableDefinitionEntity.
/// </summary>
public sealed class VariableDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets OrchestrationVersionId.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }
    /// <summary>
    /// Gets or sets Key.
    /// </summary>
    public string Key { get; set; }
    /// <summary>
    /// Gets or sets DisplayName.
    /// </summary>
    public string DisplayName { get; set; }
    /// <summary>
    /// Gets or sets Description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets Scope.
    /// </summary>
    public VariableScope Scope { get; set; }
    /// <summary>
    /// Gets or sets ValueType.
    /// </summary>
    public VariableValueType ValueType { get; set; }
    /// <summary>
    /// Gets or sets DefaultValue.
    /// </summary>
    public string DefaultValue { get; set; }
    /// <summary>
    /// Gets or sets IsRequired.
    /// </summary>
    public bool IsRequired { get; set; }
    /// <summary>
    /// Gets or sets IsSensitive.
    /// </summary>
    public bool IsSensitive { get; set; }
}
