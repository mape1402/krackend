namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Stores logical event store definitions.
/// </summary>
public sealed class EventStoreOptionsCollection
{
    private readonly Dictionary<string, EventStoreOptions> _stores = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets configured stores.
    /// </summary>
    public IReadOnlyDictionary<string, EventStoreOptions> Values => _stores;

    /// <summary>
    /// Adds or replaces a logical event store.
    /// </summary>
    public EventStoreOptionsCollection Add(string name, Action<EventStoreOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var options = new EventStoreOptions { Name = name };
        configure?.Invoke(options);
        _stores[name] = options;

        return this;
    }

    /// <summary>
    /// Gets a configured store by name.
    /// </summary>
    public EventStoreOptions GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_stores.TryGetValue(name, out var store))
            return store;

        throw new InvalidOperationException($"Event store '{name}' is not configured.");
    }
}
