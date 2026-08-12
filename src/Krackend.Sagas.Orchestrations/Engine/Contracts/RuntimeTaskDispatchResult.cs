namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Protocol-neutral runtime task dispatch result.
/// </summary>
public sealed class RuntimeTaskDispatchResult
{
    /// <summary>
    /// Gets or sets a value indicating whether dispatch succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets or sets dispatch status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets broker or transport reference.
    /// </summary>
    public string ExternalReference { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets failure reason.
    /// </summary>
    public string FailureReason { get; init; } = string.Empty;
}
