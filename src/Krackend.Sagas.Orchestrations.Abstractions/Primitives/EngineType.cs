namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Identifies the engine used to evaluate conditions or transformations.
/// </summary>
public enum EngineType
{
    /// <summary>
    /// Domain specific language engine.
    /// </summary>
    DSL,

    /// <summary>
    /// Plugin-based engine.
    /// </summary>
    Plugin,

    /// <summary>
    /// Script execution engine.
    /// </summary>
    Scripting,

    /// <summary>
    /// Custom engine implementation.
    /// </summary>
    Custom
}
