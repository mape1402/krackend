namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents MessagingTaskConfigurationJsonModel.
/// </summary>
public sealed class MessagingTaskConfigurationJsonModel : TaskConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets Topic.
    /// </summary>
    public string Topic { get; set; }
    /// <summary>
    /// Gets or sets Version.
    /// </summary>
    public string Version { get; set; }
    /// <summary>
    /// Gets or sets SchemaBinding.
    /// </summary>
    public SchemaBindingJsonModel SchemaBinding { get; set; }
    /// <summary>
    /// Gets or sets request schema binding.
    /// </summary>
    public SchemaBindingJsonModel RequestSchemaBinding { get; set; }
    /// <summary>
    /// Gets or sets response schema binding.
    /// </summary>
    public SchemaBindingJsonModel ResponseSchemaBinding { get; set; }
}
