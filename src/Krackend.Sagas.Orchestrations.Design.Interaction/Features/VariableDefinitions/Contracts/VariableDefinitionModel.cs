using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for variable definition.
/// </summary>
public sealed class VariableDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the orchestration version id.
    /// </summary>
    public string OrchestrationVersionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    public VariableScope Scope { get; set; }
    /// <summary>
    /// Gets or sets the value type.
    /// </summary>
    public VariableValueType ValueType { get; set; }
    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the is required.
    /// </summary>
    public bool IsRequired { get; set; }
    /// <summary>
    /// Gets or sets the is sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }
}

