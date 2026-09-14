namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a DSL expression used by conditions and transformations.
/// </summary>
public readonly struct Expression(string Value)
{
    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value;
}
