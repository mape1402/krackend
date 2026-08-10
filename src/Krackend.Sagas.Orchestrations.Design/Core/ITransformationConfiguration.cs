namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the configuration contract used by design-time transformations.
/// </summary>
public interface ITransformationConfiguration
{
    /// <summary>
    /// Gets the engine required to execute this transformation configuration.
    /// </summary>
    EngineType Engine { get; }
}
