namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a variable available to orchestration execution and templating.
/// </summary>
public sealed class VariableDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration version id.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets scope.
    /// </summary>
    public VariableScope Scope { get; set; }

    /// <summary>
    /// Gets or sets value type.
    /// </summary>
    public VariableValueType ValueType { get; set; }

    /// <summary>
    /// Gets or sets default value.
    /// </summary>
    public string DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets is sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }
}
