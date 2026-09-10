namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents the result of resolving a schema contract.
/// </summary>
public sealed record SchemaContractResolutionResult
{
    /// <summary>
    /// Gets the resolution status.
    /// </summary>
    public SchemaContractResolutionStatus Status { get; init; }

    /// <summary>
    /// Gets the resolved snapshot when resolution succeeded.
    /// </summary>
    public SchemaContractSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets a human-readable resolution message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful resolution result.
    /// </summary>
    public static SchemaContractResolutionResult Resolved(SchemaContractSnapshot snapshot)
        => new()
        {
            Status = SchemaContractResolutionStatus.Resolved,
            Snapshot = snapshot,
            Message = "Schema contract resolved."
        };

    /// <summary>
    /// Creates a failed resolution result.
    /// </summary>
    public static SchemaContractResolutionResult Failed(SchemaContractResolutionStatus status, string message)
        => new()
        {
            Status = status,
            Message = message
        };
}
