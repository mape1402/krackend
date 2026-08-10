using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents a polymorphic transformation configuration envelope.
/// </summary>
public sealed class TransformationConfigurationEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the discriminator value.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets DSL transformation configuration.
    /// </summary>
    public DslTransformationConfigurationJsonModel Dsl { get; set; }
}
