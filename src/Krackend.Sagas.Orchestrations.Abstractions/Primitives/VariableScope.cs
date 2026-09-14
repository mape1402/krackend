namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the variable scope values.
/// </summary>
public enum VariableScope
{
    /// <summary>
    /// Represents definition.
    /// </summary>
    Definition,
    /// <summary>
    /// Represents environment.
    /// </summary>
    Environment,
    /// <summary>
    /// Represents instance.
    /// </summary>
    Instance,
    /// <summary>
    /// Represents system.
    /// </summary>
    System
}
