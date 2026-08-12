namespace Krackend.Sagas.Orchestrations.Engine;

using System.Text.Json.Nodes;

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
    /// Gets the dispatch type declared by design.
    /// </summary>
    public string DispatchType { get; init; }

    /// <summary>
    /// Gets the parallel group identifier declared by design.
    /// </summary>
    public string ParallelGroupId { get; init; }

    /// <summary>
    /// Gets the on-error policy declared by design.
    /// </summary>
    public string OnErrorPolicy { get; init; }

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
    /// Gets the raw execution condition configuration promoted by Design.
    /// </summary>
    public JsonObject ExecutionCondition { get; init; }

    /// <summary>
    /// Gets the raw transformation configuration promoted by Design.
    /// </summary>
    public JsonObject Transformation { get; init; }

    /// <summary>
    /// Gets the raw task configuration promoted by Design.
    /// </summary>
    public JsonObject Configuration { get; init; }

    /// <summary>
    /// Gets the raw retry policy promoted by Design.
    /// </summary>
    public JsonObject RetryPolicy { get; init; }

    /// <summary>
    /// Gets the raw timeout policy promoted by Design.
    /// </summary>
    public JsonObject TimeoutPolicy { get; init; }

    /// <summary>
    /// Gets the raw compensation configuration promoted by Design.
    /// </summary>
    public JsonObject Compensation { get; init; }

    /// <summary>
    /// Gets a value indicating whether the task is a Beta 1 messaging task.
    /// </summary>
    public bool IsMessaging
        => string.Equals(Kind, "Messaging", StringComparison.OrdinalIgnoreCase)
            || Kind == "0";
}
