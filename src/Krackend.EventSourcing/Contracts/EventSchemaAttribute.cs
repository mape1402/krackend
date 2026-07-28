namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Defines the persisted event schema name and version used for an event CLR type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventSchemaAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSchemaAttribute"/> class.
    /// </summary>
    public EventSchemaAttribute(string name)
        : this(name, "1.0.0")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSchemaAttribute"/> class.
    /// </summary>
    public EventSchemaAttribute(string name, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Version = SemanticVersion.Parse(version);
    }

    /// <summary>
    /// Gets the persisted event schema name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the event schema version.
    /// </summary>
    public SemanticVersion Version { get; }
}
