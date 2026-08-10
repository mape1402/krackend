namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the variable value type values.
/// </summary>
public enum VariableValueType
{
    /// <summary>
    /// Represents string.
    /// </summary>
    String,
    
    /// <summary>
    /// Represents number.
    /// </summary>
    Number,

    /// <summary>
    /// Represents a decimal number with 28-29 significant digits and is suitable for financial and monetary
    /// calculations.
    /// </summary>
    /// <remarks>The Decimal type provides a high level of precision and is particularly useful in scenarios
    /// where rounding errors are unacceptable, such as in financial applications. It supports a wide range of values,
    /// including very small and very large numbers, while maintaining accuracy.</remarks>
    Decimal,

    /// <summary>
    /// Represents boolean.
    /// </summary>
    Boolean,
    
    /// <summary>
    /// Represents json.
    /// </summary>
    Json,
    
    /// <summary>
    /// Represents date time utc.
    /// </summary>
    DateTimeUtc,

    /// <summary>
    /// Represents a time interval, typically used to measure durations or time spans.
    /// </summary>
    /// <remarks>The TimeSpan structure can represent a duration of time in days, hours, minutes, seconds, and
    /// milliseconds. It is commonly used in scenarios where time calculations are necessary, such as measuring elapsed
    /// time or scheduling tasks.</remarks>
    TimeSpan,

    /// <summary>
    /// Represents reference.
    /// </summary>
    Reference,
    
    /// <summary>
    /// Represents secret reference.
    /// </summary>
    SecretReference
}
