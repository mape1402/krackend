namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the configuration contract used by design-time payload validations.
/// </summary>
public interface IValidationConfiguration
{
    /// <summary>
    /// Gets the engine required to execute this validation configuration.
    /// </summary>
    EngineType Engine { get; }
}
