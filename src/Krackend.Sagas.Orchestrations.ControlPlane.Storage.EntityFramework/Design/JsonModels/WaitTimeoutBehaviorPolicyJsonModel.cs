using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents WaitTimeoutBehaviorPolicyJsonModel.
/// </summary>
public sealed class WaitTimeoutBehaviorPolicyJsonModel : TimeoutBehaviorPolicyJsonModel
{
    /// <summary>
    /// Gets or sets OrchestrationAction.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrchestrationActionOnTimeout OrchestrationAction { get; set; }
    /// <summary>
    /// Gets or sets WaitingTime.
    /// </summary>
    public TimeSpan WaitingTime { get; set; }
}
