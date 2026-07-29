using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Creates DbContext instances for event store storage.
/// </summary>
public interface IEventStoreDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Creates a DbContext instance.
    /// </summary>
    TDbContext CreateDbContext();
}
