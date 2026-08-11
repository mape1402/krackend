namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Result returned after publishing orchestration output.
/// </summary>
public sealed class OrchestrationPublishResult
{
    /// <summary>
    /// Gets a value indicating whether the publish operation succeeded.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the broker or infrastructure status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the external broker reference when available.
    /// </summary>
    public string ExternalReference { get; init; }

    /// <summary>
    /// Gets the failure reason when the publish operation failed.
    /// </summary>
    public string FailureReason { get; init; }
}
