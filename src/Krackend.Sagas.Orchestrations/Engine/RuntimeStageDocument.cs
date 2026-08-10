namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime-readable stage definition extracted from a promoted artifact.
/// </summary>
internal sealed class RuntimeStageDocument
{
    /// <summary>
    /// Gets the stable stage key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets the stage execution order.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the executable tasks for the stage.
    /// </summary>
    public IReadOnlyCollection<RuntimeTaskDocument> Tasks { get; init; } = Array.Empty<RuntimeTaskDocument>();
}
