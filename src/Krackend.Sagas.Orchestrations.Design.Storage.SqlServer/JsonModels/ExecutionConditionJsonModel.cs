using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents ExecutionConditionJsonModel.
/// </summary>
public sealed class ExecutionConditionJsonModel
{
    /// <summary>
    /// Gets or sets whether the condition is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets Engine.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EngineType Engine { get; set; }

    /// <summary>
    /// Supports legacy camelCase payloads.
    /// </summary>
    [JsonPropertyName("engine")]
    public EngineType EngineLegacy
    {
        set => Engine = value;
    }
    /// <summary>
    /// Gets or sets Configuration.
    /// </summary>
    public ConditionConfigurationEnvelopeJsonModel Configuration { get; set; }

    /// <summary>
    /// Supports legacy camelCase payloads.
    /// </summary>
    [JsonPropertyName("configuration")]
    public ConditionConfigurationEnvelopeJsonModel ConfigurationLegacy
    {
        set
        {
            if (Configuration is null && value is not null)
            {
                Configuration = value;
            }
        }
    }
}
