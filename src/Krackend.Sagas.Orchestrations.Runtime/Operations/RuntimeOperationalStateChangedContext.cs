namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Context emitted when the runtime operational state changes.
/// </summary>
public sealed class RuntimeOperationalStateChangedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeOperationalStateChangedContext"/> class.
    /// </summary>
    public RuntimeOperationalStateChangedContext(
        RuntimeOperationalSnapshot previous,
        RuntimeOperationalSnapshot current)
    {
        Previous = previous;
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    /// <summary>
    /// Gets the previous snapshot, if one existed.
    /// </summary>
    public RuntimeOperationalSnapshot Previous { get; }

    /// <summary>
    /// Gets the current snapshot.
    /// </summary>
    public RuntimeOperationalSnapshot Current { get; }
}
