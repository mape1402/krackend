namespace Krackend.Sagas.Orchestrations.Engine;

using System.Text.Json.Nodes;

/// <summary>
/// Runtime-readable stage definition extracted from a promoted artifact.
/// </summary>
internal sealed class RuntimeStageDocument
{
    /// <summary>
    /// Gets the design stage identifier.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the stable stage key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets the stage execution order.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the raw execution condition configuration promoted by Design.
    /// </summary>
    public JsonObject ExecutionCondition { get; init; }

    /// <summary>
    /// Gets raw parallel group definitions promoted by Design.
    /// </summary>
    public IReadOnlyCollection<JsonObject> ParallelGroups { get; init; } = Array.Empty<JsonObject>();

    /// <summary>
    /// Gets raw branch rules promoted by Design.
    /// </summary>
    public IReadOnlyCollection<JsonObject> BranchRules { get; init; } = Array.Empty<JsonObject>();

    /// <summary>
    /// Gets the executable tasks for the stage.
    /// </summary>
    public IReadOnlyCollection<RuntimeTaskDocument> Tasks { get; init; } = Array.Empty<RuntimeTaskDocument>();
}
