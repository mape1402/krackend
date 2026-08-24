namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents PluginTaskConfigurationJsonModel.
/// </summary>
public sealed class PluginTaskConfigurationJsonModel : TaskConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets PluginId.
    /// </summary>
    public string PluginId { get; set; }
}
