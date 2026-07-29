namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;

/// <summary>
/// Describes how a state type is stored in snapshots.
/// </summary>
public sealed record StateSchemaRegistration(Type ClrType, string StateType, SemanticVersion StateSchemaVersion);
