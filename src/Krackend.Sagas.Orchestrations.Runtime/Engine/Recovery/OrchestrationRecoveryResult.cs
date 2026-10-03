namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Represents the result of an operator-driven recovery operation.
/// </summary>
public sealed record OrchestrationRecoveryResult(
    bool Succeeded,
    string InstanceId,
    string Status,
    string Message)
{
    /// <summary>
    /// Creates a successful recovery result.
    /// </summary>
    public static OrchestrationRecoveryResult Success(string instanceId, string status, string message)
        => new(true, instanceId, status, message);

    /// <summary>
    /// Creates a rejected recovery result.
    /// </summary>
    public static OrchestrationRecoveryResult Rejected(string instanceId, string status, string message)
        => new(false, instanceId, status, message);
}
