using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

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
