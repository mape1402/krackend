namespace Krackend.EventSourcing.Envelopes;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Default immutable event envelope used by event stores.
/// </summary>
public sealed record EventEnvelope(
    Guid EventId,
    string StreamName,
    string StreamId,
    string? StreamType,
    long StreamVersion,
    long? GlobalPosition,
    string EventType,
    SemanticVersion EventSchemaVersion,
    DateTimeOffset OccurredAt,
    string? CorrelationId,
    string? CausationId,
    string? UserId,
    string? TenantId,
    string? Source,
    string Payload,
    string? Metadata) : IEventEnvelope;
