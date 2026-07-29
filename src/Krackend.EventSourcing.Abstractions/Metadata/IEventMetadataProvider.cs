namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Provides dynamic metadata values when events are committed.
/// </summary>
public interface IEventMetadataProvider
{
    /// <summary>
    /// Gets the metadata key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the current metadata value.
    /// </summary>
    object? GetValue(IServiceProvider serviceProvider);
}
