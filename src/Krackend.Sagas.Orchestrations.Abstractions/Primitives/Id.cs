namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a strongly typed ULID identifier used across orchestration models.
/// </summary>
public readonly record struct Id(Ulid Value) : IComparable<Id>, IComparable
{
    /// <summary>
    /// Executes new.
    /// </summary>
    public static Id New() => new(Ulid.NewUlid());

    /// <summary>
    /// Compares this identifier with another identifier.
    /// </summary>
    /// <param name="other">Identifier to compare against.</param>
    /// <returns>A signed integer that indicates the relative order.</returns>
    public int CompareTo(Id other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Compares this identifier with another object.
    /// </summary>
    /// <param name="obj">Object to compare against.</param>
    /// <returns>A signed integer that indicates the relative order.</returns>
    public int CompareTo(object obj)
    {
        if (obj is null)
        {
            return 1;
        }

        return obj is Id other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(Id)}.", nameof(obj));
    }

    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value.ToString();
}
