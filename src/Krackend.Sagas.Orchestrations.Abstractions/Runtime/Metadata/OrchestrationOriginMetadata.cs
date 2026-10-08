namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

using System.Text.Json.Nodes;

/// <summary>
/// Carries non-controlling trace metadata describing where an emitted event originated.
/// </summary>
public sealed class OrchestrationOriginMetadata
{
    /// <summary>
    /// Gets or sets the business saga id that originated the event.
    /// </summary>
    public string SagaId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance id that originated the event.
    /// </summary>
    public string OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation id observed at the origin.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the trace id observed at the origin.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the stage key observed at the origin.
    /// </summary>
    public string StageKey { get; set; }

    /// <summary>
    /// Gets or sets the task keys observed at the origin.
    /// </summary>
    public string[] TaskKeys { get; set; }

    /// <summary>
    /// Gets or sets the task execution id observed at the origin.
    /// </summary>
    public string TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch id observed at the origin.
    /// </summary>
    public string DispatchId { get; set; }

    /// <summary>
    /// Gets or sets the execution attempt observed at the origin.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the origin source kind.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets a value indicating whether this origin has meaningful values.
    /// </summary>
    public bool HasValues =>
        !string.IsNullOrWhiteSpace(SagaId) ||
        !string.IsNullOrWhiteSpace(OrchestrationInstanceId) ||
        !string.IsNullOrWhiteSpace(CorrelationId) ||
        !string.IsNullOrWhiteSpace(TraceId) ||
        !string.IsNullOrWhiteSpace(StageKey) ||
        TaskKeys is { Length: > 0 } ||
        !string.IsNullOrWhiteSpace(TaskExecutionId) ||
        !string.IsNullOrWhiteSpace(DispatchId) ||
        Attempt > 0 ||
        !string.IsNullOrWhiteSpace(Source);

    /// <summary>
    /// Converts this metadata to a JSON payload.
    /// </summary>
    /// <returns>JSON object containing only populated values.</returns>
    public JsonObject ToJson()
    {
        var payload = new JsonObject();
        AddString(payload, nameof(SagaId), SagaId);
        AddString(payload, nameof(OrchestrationInstanceId), OrchestrationInstanceId);
        AddString(payload, nameof(CorrelationId), CorrelationId);
        AddString(payload, nameof(TraceId), TraceId);
        AddString(payload, nameof(StageKey), StageKey);
        AddStringArray(payload, nameof(TaskKeys), TaskKeys);
        AddString(payload, nameof(TaskExecutionId), TaskExecutionId);
        AddString(payload, nameof(DispatchId), DispatchId);
        if (Attempt > 0)
        {
            payload[nameof(Attempt)] = JsonValue.Create(Attempt);
        }

        AddString(payload, nameof(Source), Source);
        return payload;
    }

    private static void AddString(JsonObject payload, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload[key] = JsonValue.Create(value);
        }
    }

    private static void AddStringArray(JsonObject payload, string key, string[] values)
    {
        if (values is not { Length: > 0 })
        {
            return;
        }

        var array = new JsonArray();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                array.Add(value);
            }
        }

        if (array.Count > 0)
        {
            payload[key] = array;
        }
    }
}
