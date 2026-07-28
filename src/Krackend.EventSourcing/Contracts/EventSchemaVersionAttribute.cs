namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Defines the schema version used when an event type is persisted.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventSchemaVersionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSchemaVersionAttribute"/> class.
    /// </summary>
    public EventSchemaVersionAttribute(string version)
    {
        Version = SemanticVersion.Parse(version);
    }

    /// <summary>
    /// Gets the event schema version.
    /// </summary>
    public SemanticVersion Version { get; }
}
