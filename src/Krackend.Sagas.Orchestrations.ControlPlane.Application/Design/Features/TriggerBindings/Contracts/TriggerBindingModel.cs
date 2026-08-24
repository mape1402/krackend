using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction data for trigger binding.
/// </summary>
public sealed class TriggerBindingModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the orchestration version id.
    /// </summary>
    public string OrchestrationVersionId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public TriggerType TriggerType { get; set; }
    /// <summary>
    /// Gets or sets the trigger channel.
    /// </summary>
    public ITriggerChannel TriggerChannel { get; set; } = null!;
    /// <summary>
    /// Gets or sets the is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

