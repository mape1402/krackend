namespace Krackend.EventSourcing.Registry;

/// <summary>
/// Describes how an event type is stored.
/// </summary>
public sealed record EventTypeRegistration(Type ClrType, string EventType, int EventVersion);
