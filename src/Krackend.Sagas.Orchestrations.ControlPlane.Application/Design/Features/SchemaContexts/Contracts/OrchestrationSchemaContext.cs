namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Describes the schemas available to a task and the schema expected as its output.
/// </summary>
public sealed record OrchestrationSchemaContext
{
    /// <summary>
    /// Gets the orchestration version identifier used to build the context.
    /// </summary>
    public required string OrchestrationVersionId { get; init; }

    /// <summary>
    /// Gets the orchestration semantic version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the target stage key.
    /// </summary>
    public required string StageKey { get; init; }

    /// <summary>
    /// Gets the target task key.
    /// </summary>
    public required string TaskKey { get; init; }

    /// <summary>
    /// Gets the deterministic signature of all source and target schemas.
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Gets source schemas available to the target task.
    /// </summary>
    public IReadOnlyList<OrchestrationSchemaSource> Sources { get; init; } = Array.Empty<OrchestrationSchemaSource>();

    /// <summary>
    /// Gets the command request schema target for the target task when available.
    /// </summary>
    public OrchestrationSchemaTarget Target { get; init; }
}
