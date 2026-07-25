namespace Krackend.EventSourcing.Stores.Relational;

/// <summary>
/// Provides SQL statements for a relational event store dialect.
/// </summary>
public interface IEventStoreSqlDialect
{
    /// <summary>
    /// Creates SQL that selects the current stream version with an update lock.
    /// </summary>
    string SelectStreamVersionSql(string qualifiedTableName);

    /// <summary>
    /// Creates SQL that inserts one event and returns its global position.
    /// </summary>
    string InsertEventSql(string qualifiedTableName);

    /// <summary>
    /// Creates SQL that loads a stream.
    /// </summary>
    string LoadStreamSql(string qualifiedTableName);

    /// <summary>
    /// Creates SQL that reads events after a global position.
    /// </summary>
    string ReadFromSql(string qualifiedTableName);
}
