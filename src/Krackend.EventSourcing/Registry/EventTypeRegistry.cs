namespace Krackend.EventSourcing.Registry;

using Krackend.EventSourcing.Contracts;
using System.Reflection;

/// <summary>
/// Default in-memory event type registry.
/// </summary>
public sealed class EventTypeRegistry : IEventTypeRegistry
{
    private readonly Dictionary<Type, EventTypeRegistration> _byClrType = [];
    private readonly Dictionary<EventTypeKey, Type> _byStoredType = [];

    /// <summary>
    /// Registers an event type using its <see cref="EventSchemaAttribute"/> or explicit schema values.
    /// </summary>
    public EventTypeRegistry Register<TEvent>(string? eventType = null, SemanticVersion eventSchemaVersion = default)
        => Register(typeof(TEvent), eventType, eventSchemaVersion);

    /// <summary>
    /// Registers an event type using its <see cref="EventSchemaAttribute"/> or explicit schema values.
    /// </summary>
    public EventTypeRegistry Register(Type clrType, string? eventType = null, SemanticVersion eventSchemaVersion = default)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var schema = clrType.GetCustomAttribute<EventSchemaAttribute>();
        var hasExplicitEventType = !string.IsNullOrWhiteSpace(eventType);

        if (schema is null && !hasExplicitEventType)
            throw new InvalidOperationException($"Event type '{clrType.FullName}' must be decorated with '{nameof(EventSchemaAttribute)}' or registered with an explicit event type name.");

        if (eventSchemaVersion == default)
            eventSchemaVersion = schema?.Version ?? SemanticVersion.Default;

        var resolvedEventType = hasExplicitEventType ? eventType! : schema!.Name;
        var registration = new EventTypeRegistration(clrType, resolvedEventType, eventSchemaVersion);
        var key = new EventTypeKey(resolvedEventType, eventSchemaVersion);

        if (_byStoredType.TryGetValue(key, out var registeredType) && registeredType != clrType)
        {
            throw new InvalidOperationException(
                $"Event schema '{resolvedEventType}' version '{eventSchemaVersion}' is already registered for '{registeredType.FullName}'.");
        }

        _byClrType[clrType] = registration;
        _byStoredType[key] = clrType;

        return this;
    }

    /// <inheritdoc />
    public EventTypeRegistration GetRegistration(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (!_byClrType.TryGetValue(eventType, out var registration))
            throw new InvalidOperationException($"Event type '{eventType.FullName}' is not registered.");

        return registration;
    }

    /// <inheritdoc />
    public Type Resolve(string eventType, SemanticVersion eventSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        if (eventSchemaVersion == default)
            eventSchemaVersion = SemanticVersion.Default;

        if (_byStoredType.TryGetValue(new EventTypeKey(eventType, eventSchemaVersion), out var clrType))
            return clrType;

        throw new InvalidOperationException($"Event type '{eventType}' schema version '{eventSchemaVersion}' is not registered.");
    }

    private readonly record struct EventTypeKey(string EventType, SemanticVersion EventSchemaVersion);
}
