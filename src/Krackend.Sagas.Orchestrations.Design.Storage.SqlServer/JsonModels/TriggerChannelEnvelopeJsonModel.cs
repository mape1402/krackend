using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents a polymorphic trigger channel envelope.
/// </summary>
public sealed class TriggerChannelEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the discriminator value.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets event trigger channel payload.
    /// </summary>
    public EventTriggerChannelJsonModel Event { get; set; }
}
