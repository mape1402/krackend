namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result returned by the runtime engine after processing an intake item.
/// </summary>
public sealed class RuntimeEngineProcessResult
{
    /// <summary>
    /// Gets a value indicating whether the engine item was processed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the final runtime status for the processing attempt.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the user-readable processing result.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the processed intake buffer item identifier.
    /// </summary>
    public string BufferItemId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the persisted trigger intake identifier.
    /// </summary>
    public string IntakeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the orchestration instance identifier created by the runtime.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;
}
