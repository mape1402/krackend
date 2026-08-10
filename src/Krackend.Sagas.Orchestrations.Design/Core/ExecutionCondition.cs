namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a condition that determines whether a stage or task should execute.
/// </summary>
public sealed class ExecutionCondition
{
    /// <summary>
    /// Gets or sets engine.
    /// </summary>
    public required EngineType Engine { get; set; }

    /// <summary>
    /// Gets or sets configuration.
    /// </summary>
    public required IConditionConfiguration Configuration { get; set; }
}
