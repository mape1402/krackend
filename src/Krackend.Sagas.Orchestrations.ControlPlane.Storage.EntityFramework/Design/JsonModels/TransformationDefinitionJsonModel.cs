using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents TransformationDefinitionJsonModel.
/// </summary>
public sealed class TransformationDefinitionJsonModel
{
    /// <summary>
    /// Gets or sets whether the transformation is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets Engine.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EngineType Engine { get; set; }
    /// <summary>
    /// Gets or sets Configuration.
    /// </summary>
    public TransformationConfigurationEnvelopeJsonModel Configuration { get; set; }
}
