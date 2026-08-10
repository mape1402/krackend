using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents a polymorphic retry strategy envelope.
/// </summary>
public sealed class RetryStrategyEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the discriminator value.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets fixed retry strategy payload.
    /// </summary>
    public FixedRetryStrategyJsonModel Fixed { get; set; }
}
