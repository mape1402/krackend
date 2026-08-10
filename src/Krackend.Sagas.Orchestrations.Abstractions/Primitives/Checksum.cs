namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a checksum value used to verify artifact integrity.
/// </summary>
public readonly struct Checksum
{
    /// <summary>
    /// Initializes a new instance of the Checksum class using the specified checksum value.
    /// </summary>
    /// <param name="value">The checksum value represented as a string. This parameter cannot be null or empty.</param>
    public Checksum(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Checksum value cannot be null or empty.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the string value represented by this property.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value;
}
