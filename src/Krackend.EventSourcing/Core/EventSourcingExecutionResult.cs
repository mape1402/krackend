using Krackend.EventSourcing.Envelopes;

namespace Krackend.EventSourcing.Core;

/// <summary>
/// Represents the result of an event sourced command execution.
/// </summary>
public sealed record EventSourcingExecutionResult<TState>(
    TState PreviousState,
    TState CurrentState,
    long PreviousVersion,
    long CurrentVersion,
    IReadOnlyCollection<EventEnvelope> CommittedEvents);
