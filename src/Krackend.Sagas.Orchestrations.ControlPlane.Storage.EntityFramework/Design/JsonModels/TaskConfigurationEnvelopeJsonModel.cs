using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a polymorphic task configuration envelope for JSON persistence.
/// </summary>
public sealed class TaskConfigurationEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the polymorphic discriminator.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets HTTP task configuration payload.
    /// </summary>
    public HttpTaskConfigurationJsonModel Http { get; set; }

    /// <summary>
    /// Gets or sets messaging task configuration payload.
    /// </summary>
    public MessagingTaskConfigurationJsonModel Messaging { get; set; }

    /// <summary>
    /// Gets or sets plugin task configuration payload.
    /// </summary>
    public PluginTaskConfigurationJsonModel Plugin { get; set; }

    /// <summary>
    /// Gets or sets human approval task configuration payload.
    /// </summary>
    public HumanApprovalTaskConfigurationJsonModel HumanApproval { get; set; }
}
