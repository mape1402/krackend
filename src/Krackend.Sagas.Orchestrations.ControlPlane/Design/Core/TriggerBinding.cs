namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a trigger binding that can start an orchestration version.
/// </summary>
public sealed class TriggerBinding
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration version id.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets trigger type.
    /// </summary>
    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// Gets or sets trigger channel.
    /// </summary>
    public required ITriggerChannel TriggerChannel { get; set; }

    /// <summary>
    /// Gets or sets is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
