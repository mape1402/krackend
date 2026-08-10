using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;

/// <summary>
/// Represents Security storage DbContext.
/// </summary>
public sealed class SecurityStorageDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">Context options.</param>
    public SecurityStorageDbContext(DbContextOptions<SecurityStorageDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets teams set.
    /// </summary>
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    /// <summary>
    /// Gets team members set.
    /// </summary>
    public DbSet<TeamMemberEntity> TeamMembers => Set<TeamMemberEntity>();

    /// <summary>
    /// Configures model mappings.
    /// </summary>
    /// <param name="modelBuilder">Model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Security");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecurityStorageDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
