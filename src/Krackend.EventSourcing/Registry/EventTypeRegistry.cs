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
    public EventTypeRegistry Register<TEvent>(string? eventType = null, string eventSchemaVersion = "1.0.0")
        => Register(typeof(TEvent), eventType, eventSchemaVersion);

    /// <summary>
    /// Registers an event type.
    /// </summary>
    public EventTypeRegistry Register(Type clrType, string? eventType = null, string eventSchemaVersion = "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventSchemaVersion);

        var resolvedEventType = string.IsNullOrWhiteSpace(eventType) ? clrType.Name : eventType;
        var registration = new EventTypeRegistration(clrType, resolvedEventType, eventSchemaVersion);

        _byClrType[clrType] = registration;
        _byStoredType[new EventTypeKey(resolvedEventType, eventSchemaVersion)] = clrType;

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
    public Type Resolve(string eventType, string eventSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventSchemaVersion);

        if (_byStoredType.TryGetValue(new EventTypeKey(eventType, eventSchemaVersion), out var clrType))
            return clrType;

        throw new InvalidOperationException($"Event type '{eventType}' schema version '{eventSchemaVersion}' is not registered.");
    }

    private readonly record struct EventTypeKey(string EventType, string EventSchemaVersion);
}
