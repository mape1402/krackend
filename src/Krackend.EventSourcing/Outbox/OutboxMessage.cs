namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// Represents an integration event waiting to be published.
/// </summary>
public sealed record OutboxMessage(
    Guid MessageId,
    string EventType,
    int EventVersion,
    string Payload,
    string? Metadata,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt = null);
