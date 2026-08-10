namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

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
