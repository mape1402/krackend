namespace Krackend.EventSourcing.Snapshots;

/// <summary>
/// Serializes aggregate snapshots.
/// </summary>
public interface ISnapshotSerializer
{
    /// <summary>
    /// Serializes a snapshot state object.
    /// </summary>
    string Serialize<T>(T value);

    /// <summary>
    /// Deserializes a snapshot state object.
    /// </summary>
    object? Deserialize(string value, Type targetType);
}
