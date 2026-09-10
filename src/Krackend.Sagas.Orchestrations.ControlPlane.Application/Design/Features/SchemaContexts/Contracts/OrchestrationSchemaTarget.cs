namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Represents the target schema expected from a transform or validation operation.
/// </summary>
public sealed record OrchestrationSchemaTarget
{
    /// <summary>
    /// Gets the target alias.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the stage key of the target task.
    /// </summary>
    public required string StageKey { get; init; }

    /// <summary>
    /// Gets the task key of the target task.
    /// </summary>
    public required string TaskKey { get; init; }

    /// <summary>
    /// Gets the schema binding expected by the target operation.
    /// </summary>
    public SchemaBinding SchemaBinding { get; init; }
}
