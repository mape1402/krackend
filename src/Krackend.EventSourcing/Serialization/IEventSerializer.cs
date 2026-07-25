namespace Krackend.EventSourcing.Serialization;

/// <summary>
/// Serializes and deserializes event payloads and metadata.
/// </summary>
public interface IEventSerializer
{
    /// <summary>
    /// Serializes a value into its persisted representation.
    /// </summary>
    string Serialize<T>(T value);

    /// <summary>
    /// Deserializes a persisted value into the specified type.
    /// </summary>
    object? Deserialize(string value, Type targetType);
}
