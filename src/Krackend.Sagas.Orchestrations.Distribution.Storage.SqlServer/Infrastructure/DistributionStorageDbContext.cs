using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;

public sealed class DistributionStorageDbContext : DbContext
{
    public DistributionStorageDbContext(DbContextOptions<DistributionStorageDbContext> options) : base(options) { }

    public DbSet<EnvironmentEntity> Environments => Set<EnvironmentEntity>();
    public DbSet<RuntimeNodeEntity> RuntimeNodes => Set<RuntimeNodeEntity>();
    public DbSet<RuntimeCapabilityEntity> RuntimeCapabilities => Set<RuntimeCapabilityEntity>();
    public DbSet<OrchestrationProjectionEntity> OrchestrationProjections => Set<OrchestrationProjectionEntity>();
    public DbSet<OrchestrationAllowedRuntimeNodeEntity> OrchestrationAllowedRuntimeNodes => Set<OrchestrationAllowedRuntimeNodeEntity>();
    public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();
    public DbSet<ReleaseEntity> Releases => Set<ReleaseEntity>();
    public DbSet<ReleasePlanTargetEntity> ReleasePlanTargets => Set<ReleasePlanTargetEntity>();
    public DbSet<ReleaseTargetEntity> ReleaseTargets => Set<ReleaseTargetEntity>();
    public DbSet<ReleaseAttemptEntity> ReleaseAttempts => Set<ReleaseAttemptEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Distribution");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DistributionStorageDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}


