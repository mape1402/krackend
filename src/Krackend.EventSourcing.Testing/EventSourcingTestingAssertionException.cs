namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Represents a failed event sourcing testing assertion.
/// </summary>
public sealed class EventSourcingTestingAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcingTestingAssertionException"/> class.
    /// </summary>
    public EventSourcingTestingAssertionException(string message)
        : base(message)
    {
    }
}
