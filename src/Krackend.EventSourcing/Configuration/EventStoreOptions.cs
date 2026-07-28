namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Configures a logical event store destination.
/// </summary>
public sealed class EventStoreOptions
{
    /// <summary>
    /// Gets the logical store name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the database schema.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Gets or sets the physical table name.
    /// </summary>
    public string TableName { get; set; } = "Events";

    /// <summary>
    /// Gets the fully qualified table name.
    /// </summary>
    public string QualifiedTableName
        => string.IsNullOrWhiteSpace(Schema) ? TableName : $"{Schema}.{TableName}";
}
