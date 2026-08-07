namespace Krackend.Testing;

/// <summary>
/// Represents a failed Krackend testing assertion.
/// </summary>
public sealed class KrackendTestingAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendTestingAssertionException"/> class.
    /// </summary>
    public KrackendTestingAssertionException(string message)
        : base(message)
    {
    }
}
