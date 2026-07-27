namespace Krackend.EventSourcing.Core;

/// <summary>
/// Represents rehydrated state and stream version.
/// </summary>
public sealed record EventSourcedState<TState>(TState State, long Version);
