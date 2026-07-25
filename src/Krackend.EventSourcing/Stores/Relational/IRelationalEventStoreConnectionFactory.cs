using System.Data.Common;

namespace Krackend.EventSourcing.Stores.Relational;

/// <summary>
/// Creates database connections for the relational event store.
/// </summary>
public interface IRelationalEventStoreConnectionFactory
{
    /// <summary>
    /// Creates a database connection.
    /// </summary>
    ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
