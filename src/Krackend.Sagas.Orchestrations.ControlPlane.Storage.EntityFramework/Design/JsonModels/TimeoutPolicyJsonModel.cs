using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents TimeoutPolicyJsonModel.
/// </summary>
public sealed class TimeoutPolicyJsonModel
{
    /// <summary>
    /// Gets or sets Timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; }
    /// <summary>
    /// Gets or sets TimeoutBehavior.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TimeoutBehavior TimeoutBehavior { get; set; }
    /// <summary>
    /// Gets or sets TimeoutBehaviorPolicy.
    /// </summary>
    public TimeoutBehaviorPolicyEnvelopeJsonModel TimeoutBehaviorPolicy { get; set; }
}
