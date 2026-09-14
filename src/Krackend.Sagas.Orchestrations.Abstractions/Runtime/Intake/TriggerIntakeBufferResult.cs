using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

#nullable enable

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Represents the result of an intake buffer operation.
/// </summary>
public sealed class TriggerIntakeBufferResult
{
    private TriggerIntakeBufferResult(bool accepted, Id? bufferItemId, string? reason)
    {
        Accepted = accepted;
        BufferItemId = bufferItemId;
        Reason = reason;
    }

    /// <summary>
    /// Gets whether the operation was accepted.
    /// </summary>
    public bool Accepted { get; }

    /// <summary>
    /// Gets buffer item id when available.
    /// </summary>
    public Id? BufferItemId { get; }

    /// <summary>
    /// Gets a reason for rejection or additional operation context.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates an accepted result.
    /// </summary>
    public static TriggerIntakeBufferResult Accept(Id bufferItemId, string? reason = null)
        => new(true, bufferItemId, reason);

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static TriggerIntakeBufferResult Reject(string reason)
        => new(false, null, reason);
}
