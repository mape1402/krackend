namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a time duration used by orchestration policies.
/// </summary>
public readonly record struct Duration(TimeSpan Value)
{
    /// <summary>
    /// Executes from seconds.
    /// </summary>
    public static Duration FromSeconds(double seconds) => new(TimeSpan.FromSeconds(seconds));

    /// <summary>
    /// Executes from minutes.
    /// </summary>
    public static Duration FromMinutes(double minutes) => new(TimeSpan.FromMinutes(minutes));

    /// <summary>
    /// Returns the string representation of the current instance.
    /// </summary>
    public override string ToString() => Value.ToString();
}
