namespace Krackend.EventSourcing.Stores.Relational;

/// <summary>
/// SQL Server dialect for the relational event store.
/// </summary>
public sealed class SqlServerEventStoreSqlDialect : IEventStoreSqlDialect
{
    /// <inheritdoc />
    public string SelectStreamVersionSql(string qualifiedTableName)
        => $"""
           select coalesce(max(StreamVersion), 0)
           from {qualifiedTableName} with (updlock, holdlock)
           where StreamName = @StreamName and StreamId = @StreamId
           """;

    /// <inheritdoc />
    public string InsertEventSql(string qualifiedTableName)
        => $"""
           insert into {qualifiedTableName}
           (
               EventId,
               StreamName,
               StreamId,
               StreamType,
               StreamVersion,
               EventType,
               EventVersion,
               OccurredAt,
               Payload,
               Metadata
           )
           output inserted.GlobalPosition
           values
           (
               @EventId,
               @StreamName,
               @StreamId,
               @StreamType,
               @StreamVersion,
               @EventType,
               @EventVersion,
               @OccurredAt,
               @Payload,
               @Metadata
           )
           """;

    /// <inheritdoc />
    public string LoadStreamSql(string qualifiedTableName)
        => $"""
           select EventId, StreamName, StreamId, StreamType, StreamVersion, GlobalPosition, EventType, EventVersion, OccurredAt, Payload, Metadata
           from {qualifiedTableName}
           where StreamName = @StreamName and StreamId = @StreamId
           order by StreamVersion
           """;

    /// <inheritdoc />
    public string ReadFromSql(string qualifiedTableName)
        => $"""
           select top (@MaxCount) EventId, StreamName, StreamId, StreamType, StreamVersion, GlobalPosition, EventType, EventVersion, OccurredAt, Payload, Metadata
           from {qualifiedTableName}
           where StreamName = @StreamName and GlobalPosition > @AfterGlobalPosition
           order by GlobalPosition
           """;
}
