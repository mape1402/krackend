namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the configuration contract used by design-time execution conditions.
/// </summary>
public interface IConditionConfiguration
{
    /// <summary>
    /// Gets the engine required to evaluate this condition configuration.
    /// </summary>
    EngineType Engine { get; }
}
