namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result returned after a runtime messaging command is dispatched.
/// </summary>
public sealed class MessagingDispatchResult
{
    /// <summary>
    /// Gets a value indicating whether the message was accepted by the publisher.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the dispatcher status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the external broker or adapter reference when one exists.
    /// </summary>
    public string ExternalReference { get; init; } = string.Empty;

    /// <summary>
    /// Gets the failure reason when dispatching fails.
    /// </summary>
    public string FailureReason { get; init; } = string.Empty;
}
