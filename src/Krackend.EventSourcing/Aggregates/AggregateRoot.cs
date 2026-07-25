using System.Reflection;

namespace Krackend.EventSourcing.Aggregates;

/// <summary>
/// Optional base class for aggregates that raise and apply domain events.
/// </summary>
public abstract class AggregateRoot : IAggregateRoot
{
    private readonly List<object> _pendingEvents = [];

    /// <inheritdoc />
    public string Id { get; protected set; } = string.Empty;

    /// <inheritdoc />
    public long Version { get; private set; }

    /// <inheritdoc />
    public IReadOnlyCollection<object> PendingEvents => _pendingEvents;

    /// <inheritdoc />
    public void LoadFromHistory(IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var @event in events)
        {
            Apply(@event);
            Version++;
        }
    }

    /// <inheritdoc />
    public void ClearPendingEvents()
        => _pendingEvents.Clear();

    /// <summary>
    /// Raises a new pending domain event and applies it to the aggregate state.
    /// </summary>
    protected void Raise(object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        Apply(@event);
        _pendingEvents.Add(@event);
        Version++;
    }

    private void Apply(object @event)
    {
        var eventType = @event.GetType();
        var applyMethod = GetType().GetMethod(
            "Apply",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [eventType],
            modifiers: null);

        applyMethod?.Invoke(this, [@event]);
    }
}
