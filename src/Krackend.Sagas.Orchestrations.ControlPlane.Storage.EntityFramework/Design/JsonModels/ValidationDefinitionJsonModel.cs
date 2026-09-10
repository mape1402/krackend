using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a persisted payload validation definition.
/// </summary>
public sealed class ValidationDefinitionJsonModel
{
    /// <summary>
    /// Gets or sets whether this validation definition is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the validation engine.
    /// </summary>
    public EngineType Engine { get; set; }

    /// <summary>
    /// Gets or sets the fallback error code used when validation fails.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the validation configuration payload.
    /// </summary>
    public ValidationConfigurationEnvelopeJsonModel Configuration { get; set; }
}
