namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime-readable task definition extracted from a promoted artifact stage.
/// </summary>
internal sealed class RuntimeTaskDocument
{
    /// <summary>
    /// Gets the stable task key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets the task execution order inside the stage.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the task kind declared by design.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the task execution mode declared by design.
    /// </summary>
    public string ExecutionMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the task should participate in runtime execution.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether runtime must wait for a back-channel response.
    /// </summary>
    public bool AwaitResponse { get; init; }

    /// <summary>
    /// Gets the outbound messaging topic or queue.
    /// </summary>
    public string Destination { get; init; }

    /// <summary>
    /// Gets the message contract version configured for the messaging task.
    /// </summary>
    public string MessageVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the task is a Beta 1 messaging task.
    /// </summary>
    public bool IsMessaging
        => string.Equals(Kind, "Messaging", StringComparison.OrdinalIgnoreCase)
            || Kind == "0";
}
