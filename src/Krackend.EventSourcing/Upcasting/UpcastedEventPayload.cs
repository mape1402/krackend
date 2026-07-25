namespace Krackend.EventSourcing.Upcasting;

/// <summary>
/// Represents an upcasted event payload.
/// </summary>
public sealed record UpcastedEventPayload(string EventType, int EventVersion, string Payload);
