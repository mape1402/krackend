namespace Krackend.EventSourcing.Registry;

/// <summary>
/// Default in-memory event type registry.
/// </summary>
public sealed class EventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<Type, EventTypeRegistration> _byClrType = [];
    private readonly Dictionary<EventTypeKey, Type> _byStoredType = [];

    /// <summary>
    /// Registers an event type.
    /// </summary>
    public EventTypeRegistry Register<TEvent>(string? eventType = null, int eventVersion = 1)
        => Register(typeof(TEvent), eventType, eventVersion);

    /// <summary>
    /// Registers an event type.
    /// </summary>
    public EventTypeRegistry Register(Type clrType, string? eventType = null, int eventVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (eventVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventVersion), "Event version must be greater than zero.");

        var resolvedEventType = string.IsNullOrWhiteSpace(eventType) ? clrType.Name : eventType;
        var registration = new EventTypeRegistration(clrType, resolvedEventType, eventVersion);

        _byClrType[clrType] = registration;
        _byStoredType[new EventTypeKey(resolvedEventType, eventVersion)] = clrType;

        return this;
    }

    /// <inheritdoc />
    public EventTypeRegistration GetRegistration(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (_byClrType.TryGetValue(eventType, out var registration))
            return registration;

        return Register(eventType).GetRegistration(eventType);
    }

    /// <inheritdoc />
    public Type Resolve(string eventType, int eventVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        if (_byStoredType.TryGetValue(new EventTypeKey(eventType, eventVersion), out var clrType))
            return clrType;

        throw new InvalidOperationException($"Event type '{eventType}' version '{eventVersion}' is not registered.");
    }

    private readonly record struct EventTypeKey(string EventType, int EventVersion);
}
