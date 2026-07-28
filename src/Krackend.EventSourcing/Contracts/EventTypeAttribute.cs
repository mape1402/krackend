namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Defines the persisted event type name used for an event CLR type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventTypeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventTypeAttribute"/> class.
    /// </summary>
    public EventTypeAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Gets the persisted event type name.
    /// </summary>
    public string Name { get; }
}
