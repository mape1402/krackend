namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents FailTimeoutBehaviorPolicyJsonModel.
/// </summary>
public sealed class FailTimeoutBehaviorPolicyJsonModel : TimeoutBehaviorPolicyJsonModel
{
    /// <summary>
    /// Gets or sets ErrorCode.
    /// </summary>
    public string ErrorCode { get; set; }
}
