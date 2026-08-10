namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents EventTriggerChannelJsonModel.
/// </summary>
public sealed class EventTriggerChannelJsonModel : TriggerChannelJsonModel
{
    /// <summary>
    /// Gets or sets SchemaBinding.
    /// </summary>
    public SchemaBindingJsonModel SchemaBinding { get; set; }
    /// <summary>
    /// Gets or sets Topic.
    /// </summary>
    public string Topic { get; set; }
    /// <summary>
    /// Gets or sets Version.
    /// </summary>
    public string Version { get; set; }
}
