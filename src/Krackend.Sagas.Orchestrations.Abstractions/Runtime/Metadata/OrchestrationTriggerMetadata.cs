namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

using System.Text.Json.Nodes;

/// <summary>
/// Carries Krackend-defined metadata for the event that starts an orchestration.
/// </summary>
public sealed class OrchestrationTriggerMetadata
{
    /// <summary>
    /// Gets or sets the end-to-end correlation id used to identify the SAGA.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the trace id used to correlate distributed telemetry.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the unique event identifier.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the explicit idempotency key for trigger intake.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the aggregate identifier related to the event.
    /// </summary>
    public string AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type related to the event.
    /// </summary>
    public string AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the causation identifier.
    /// </summary>
    public string CausationId { get; set; }

    /// <summary>
    /// Creates metadata from a JSON payload.
    /// </summary>
    /// <param name="payload">JSON payload to read.</param>
    /// <returns>Parsed trigger metadata.</returns>
    public static OrchestrationTriggerMetadata FromJson(JsonNode payload)
    {
        if (payload is not JsonObject obj)
        {
            return new OrchestrationTriggerMetadata();
        }

        return new OrchestrationTriggerMetadata
        {
            CorrelationId = GetString(obj, nameof(CorrelationId)),
            TraceId = GetString(obj, nameof(TraceId)),
            EventId = GetString(obj, nameof(EventId)),
            EventType = GetString(obj, nameof(EventType)),
            IdempotencyKey = GetString(obj, nameof(IdempotencyKey)),
            AggregateId = GetString(obj, nameof(AggregateId)),
            AggregateType = GetString(obj, nameof(AggregateType)),
            CausationId = GetString(obj, nameof(CausationId))
        };
    }

    /// <summary>
    /// Converts this metadata to a JSON payload.
    /// </summary>
    /// <returns>JSON object containing only populated values.</returns>
    public JsonObject ToJson()
    {
        var payload = new JsonObject();
        AddString(payload, nameof(CorrelationId), CorrelationId);
        AddString(payload, nameof(TraceId), TraceId);
        AddString(payload, nameof(EventId), EventId);
        AddString(payload, nameof(EventType), EventType);
        AddString(payload, nameof(IdempotencyKey), IdempotencyKey);
        AddString(payload, nameof(AggregateId), AggregateId);
        AddString(payload, nameof(AggregateType), AggregateType);
        AddString(payload, nameof(CausationId), CausationId);
        return payload;
    }

    private static string GetString(JsonObject payload, string key)
    {
        if (!payload.TryGetPropertyValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        try
        {
            return value.GetValue<string>() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return value.ToString();
        }
    }

    private static void AddString(JsonObject payload, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload[key] = JsonValue.Create(value);
        }
    }
}
