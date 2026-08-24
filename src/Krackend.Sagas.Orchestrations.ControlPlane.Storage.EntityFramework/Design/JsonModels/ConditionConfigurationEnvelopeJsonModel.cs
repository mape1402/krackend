using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a polymorphic condition configuration envelope.
/// </summary>
public sealed class ConditionConfigurationEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the discriminator value.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Supports legacy discriminator key.
    /// </summary>
    [JsonPropertyName("type")]
    public string LegacyType
    {
        set
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                Type = value ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Gets or sets DSL condition configuration.
    /// </summary>
    public DslConditionConfigurationJsonModel Dsl { get; set; }

    /// <summary>
    /// Supports legacy camelCase payloads.
    /// </summary>
    [JsonPropertyName("dsl")]
    public DslConditionConfigurationJsonModel DslLegacy
    {
        set
        {
            if (Dsl is null && value is not null)
            {
                Dsl = value;
            }
        }
    }
}
