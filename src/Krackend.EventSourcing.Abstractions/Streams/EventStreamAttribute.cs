namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Declares the logical stream name used by the default command stream resolver.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventStreamAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamAttribute"/> class.
    /// </summary>
    public EventStreamAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Gets the logical stream name.
    /// </summary>
    public string Name { get; }
}
