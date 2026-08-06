using Krackend.EventSourcing.Contracts;

namespace Krackend.EventSourcing.Centralized.Sample.Requests;

public sealed record IncomingIntegrationEvent(
    string BoundedContext,
    string AggregateType,
    string AggregateId,
    string EventType,
    SemanticVersion EventSchemaVersion,
    string Payload,
    string? Metadata = null,
    long? ExpectedStreamVersion = null);
