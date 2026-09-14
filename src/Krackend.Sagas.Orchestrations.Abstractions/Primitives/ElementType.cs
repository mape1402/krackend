namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Identifies the kind of element referenced inside an orchestration graph.
/// </summary>
public enum ElementType
{
    /// <summary>
    /// A root orchestration element.
    /// </summary>
    Orchestration,

    /// <summary>
    /// A stage element.
    /// </summary>
    Stage,

    /// <summary>
    /// A task element.
    /// </summary>
    Task
}
