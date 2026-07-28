namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Describes how an event type is stored.
/// </summary>
public sealed record EventTypeRegistration(Type ClrType, string EventType, SemanticVersion EventSchemaVersion);
