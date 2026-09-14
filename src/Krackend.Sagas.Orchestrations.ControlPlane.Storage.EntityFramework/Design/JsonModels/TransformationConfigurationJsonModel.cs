using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonConverters;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

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
