namespace Krackend.Sagas.Orchestrations.Design.Core.ConditionConfigurations;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a DSL-based condition configuration used to evaluate execution conditions.
/// </summary>
public sealed class DslConditionConfiguration : IConditionConfiguration
{
    /// <summary>
    /// Gets the engine used to evaluate this condition.
    /// </summary>
    public EngineType Engine => EngineType.DSL;

    /// <summary>
    /// Gets or sets the DSL expression evaluated to determine whether execution should continue.
    /// </summary>
    public Expression Expression { get; set; }
}
