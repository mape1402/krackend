namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a strongly typed ULID identifier used across orchestration models.
/// </summary>
public readonly record struct Id(Ulid Value)
{
    /// <summary>
    /// Executes new.
    /// </summary>
    public static Id New() => new(Ulid.NewUlid());

    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value.ToString();
}
