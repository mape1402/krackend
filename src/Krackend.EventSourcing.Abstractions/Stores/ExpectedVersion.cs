namespace Krackend.EventSourcing.Stores;

/// <summary>
/// Defines the version precondition used when appending events.
/// </summary>
public readonly record struct ExpectedVersion
{
    private ExpectedVersion(ExpectedVersionMode mode, long value)
    {
        Mode = mode;
        Value = value;
    }

    /// <summary>
    /// Gets the precondition mode.
    /// </summary>
    public ExpectedVersionMode Mode { get; }

    /// <summary>
    /// Gets the expected version when <see cref="Mode"/> is <see cref="ExpectedVersionMode.Exact"/>.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Appends without a stream version precondition.
    /// </summary>
    public static ExpectedVersion Any { get; } = new(ExpectedVersionMode.Any, 0);

    /// <summary>
    /// Appends only when the stream does not exist.
    /// </summary>
    public static ExpectedVersion NoStream { get; } = new(ExpectedVersionMode.NoStream, 0);

    /// <summary>
    /// Appends only when the stream current version matches the specified value.
    /// </summary>
    public static ExpectedVersion Exact(long version)
    {
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version), "Expected version cannot be negative.");

        return new ExpectedVersion(ExpectedVersionMode.Exact, version);
    }
}
