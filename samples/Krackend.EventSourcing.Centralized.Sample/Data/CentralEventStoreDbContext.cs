using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.Centralized.Sample.Data;

public sealed class CentralEventStoreDbContext : DbContext
{
    public CentralEventStoreDbContext(DbContextOptions<CentralEventStoreDbContext> options)
        : base(options)
    {
    }
}
