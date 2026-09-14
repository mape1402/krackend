using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents ReconcileTimeoutBehaviorPolicyJsonModel.
/// </summary>
public sealed class ReconcileTimeoutBehaviorPolicyJsonModel : TimeoutBehaviorPolicyJsonModel
{
    /// <summary>
    /// Gets or sets OrchestrationAction.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrchestrationActionOnTimeout OrchestrationAction { get; set; }
    /// <summary>
    /// Gets or sets RetryPolicy.
    /// </summary>
    public RetryPolicyJsonModel RetryPolicy { get; set; }
}
