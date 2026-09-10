using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a polymorphic validation configuration envelope for JSON persistence.
/// </summary>
public sealed class ValidationConfigurationEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the polymorphic discriminator.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets DSL validation configuration payload.
    /// </summary>
    public DslValidationConfigurationJsonModel Dsl { get; set; }
}
