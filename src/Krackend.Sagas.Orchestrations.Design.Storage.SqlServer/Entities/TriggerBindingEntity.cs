using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents TriggerBindingEntity.
/// </summary>
public sealed class TriggerBindingEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets OrchestrationVersionId.
    /// </summary>
    public Id OrchestrationVersionId { get; set; }
    /// <summary>
    /// Gets or sets Key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets TriggerType.
    /// </summary>
    public TriggerType TriggerType { get; set; }
    /// <summary>
    /// Gets or sets IsEnabled.
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// Gets or sets Description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets TriggerChannel.
    /// </summary>
    public TriggerChannelEnvelopeJsonModel TriggerChannel { get; set; } = new();
}
