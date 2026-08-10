namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a string reference value used by orchestration contracts.
/// </summary>
public readonly struct Reference(string Value)
{
    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value;
}
