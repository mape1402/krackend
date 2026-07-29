namespace Krackend.EventSourcing.Contracts;

/// <summary>
/// Defines the persisted state schema name and version used for snapshots.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class StateSchemaAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateSchemaAttribute"/> class.
    /// </summary>
    public StateSchemaAttribute(string name)
        : this(name, "1.0.0")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateSchemaAttribute"/> class.
    /// </summary>
    public StateSchemaAttribute(string name, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Version = SemanticVersion.Parse(version);
    }

    /// <summary>
    /// Gets the persisted state schema name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the state schema version.
    /// </summary>
    public SemanticVersion Version { get; }
}
