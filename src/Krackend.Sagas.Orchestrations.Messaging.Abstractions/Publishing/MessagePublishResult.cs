namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

/// <summary>
/// Technical result returned by a broker-neutral publisher.
/// </summary>
public sealed class MessagePublishResult
{
    /// <summary>
    /// Gets a value indicating whether the adapter accepted the publish operation.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the adapter status for the publish operation.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets an optional broker-specific reference.
    /// </summary>
    public string ExternalReference { get; init; } = string.Empty;

    /// <summary>
    /// Gets the failure detail when <see cref="Succeeded"/> is false.
    /// </summary>
    public string FailureReason { get; init; } = string.Empty;
}
