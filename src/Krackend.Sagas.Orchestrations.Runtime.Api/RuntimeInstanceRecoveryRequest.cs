namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Represents a runtime instance recovery operation request.
/// </summary>
public sealed record RuntimeInstanceRecoveryRequest
{
    /// <summary>
    /// Gets the operator supplied reason for the recovery operation.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional replay payload override.
    /// </summary>
    public string Payload { get; init; } = string.Empty;
}
