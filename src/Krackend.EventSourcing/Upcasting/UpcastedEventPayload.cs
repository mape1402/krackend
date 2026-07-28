namespace Krackend.EventSourcing.Upcasting;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Represents an upcasted event payload.
/// </summary>
public sealed record UpcastedEventPayload(string EventType, SemanticVersion EventSchemaVersion, string Payload);
