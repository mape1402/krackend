namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Represents one schema source available to a task transform or validation.
/// </summary>
public sealed record OrchestrationSchemaSource
{
    /// <summary>
    /// Gets the stable alias used by transformations and validations.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the source category.
    /// </summary>
    public OrchestrationSchemaContextSourceKind SourceKind { get; init; }

    /// <summary>
    /// Gets the stage key when the source belongs to a stage task.
    /// </summary>
    public string StageKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the task key when the source belongs to a task response.
    /// </summary>
    public string TaskKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the schema binding associated with the source.
    /// </summary>
    public SchemaBinding SchemaBinding { get; init; }
}
