using Microsoft.EntityFrameworkCore;

namespace Krackend.EventSourcing.Sqlite.Sample.Data;

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options)
        : base(options)
    {
    }
}
