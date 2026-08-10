using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonConverters;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

[JsonConverter(typeof(TaskConfigurationJsonConverter))]
/// <summary>
/// Represents TaskConfigurationJsonModel.
/// </summary>
public class TaskConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets the polymorphic discriminator.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;
}
