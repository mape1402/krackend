namespace Krackend.EventSourcing.Diagnostics;

/// <summary>
/// Represents a Krackend EventSourcing runtime error.
/// </summary>
public abstract class EventSourcingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcingException"/> class.
    /// </summary>
    protected EventSourcingException(string message)
        : base(message)
    {
    }
}
