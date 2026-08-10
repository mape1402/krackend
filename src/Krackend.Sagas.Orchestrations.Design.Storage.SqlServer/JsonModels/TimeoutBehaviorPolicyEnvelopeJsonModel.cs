using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents a polymorphic timeout behavior policy envelope.
/// </summary>
public sealed class TimeoutBehaviorPolicyEnvelopeJsonModel
{
    /// <summary>
    /// Gets or sets the discriminator value.
    /// </summary>
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets fail timeout behavior payload.
    /// </summary>
    public FailTimeoutBehaviorPolicyJsonModel Fail { get; set; }

    /// <summary>
    /// Gets or sets wait timeout behavior payload.
    /// </summary>
    public WaitTimeoutBehaviorPolicyJsonModel Wait { get; set; }

    /// <summary>
    /// Gets or sets reconcile timeout behavior payload.
    /// </summary>
    public ReconcileTimeoutBehaviorPolicyJsonModel Reconcile { get; set; }
}
