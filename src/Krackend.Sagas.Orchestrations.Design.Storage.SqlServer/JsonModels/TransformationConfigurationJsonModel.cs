using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

[JsonConverter(typeof(TransformationConfigurationJsonConverter))]
/// <summary>
/// Represents TransformationConfigurationJsonModel.
/// </summary>
public class TransformationConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets the polymorphic discriminator.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;
}
