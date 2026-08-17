using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;
using Mule;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

public sealed class RuntimeStorageDbContext : DbContext
{
    private int _deferAutoSaveDepth;

    public RuntimeStorageDbContext(DbContextOptions<RuntimeStorageDbContext> options)
        : base(options)
    {
    }

    public bool AutoSaveChanges => _deferAutoSaveDepth == 0;

    public DbSet<RuntimeArtifactEntity> Artifacts => Set<RuntimeArtifactEntity>();
    public DbSet<TriggerIntakeEntity> TriggerIntakes => Set<TriggerIntakeEntity>();
    public DbSet<TriggerIntakeAttemptEntity> TriggerIntakeAttempts => Set<TriggerIntakeAttemptEntity>();
    public DbSet<OrchestrationInstanceEntity> OrchestrationInstances => Set<OrchestrationInstanceEntity>();
    public DbSet<StageExecutionEntity> StageExecutions => Set<StageExecutionEntity>();
    public DbSet<TaskExecutionEntity> TaskExecutions => Set<TaskExecutionEntity>();
    public DbSet<TaskExecutionAttemptEntity> TaskExecutionAttempts => Set<TaskExecutionAttemptEntity>();
    public DbSet<TaskDispatchEntity> TaskDispatches => Set<TaskDispatchEntity>();
    public DbSet<CompensationExecutionEntity> CompensationExecutions => Set<CompensationExecutionEntity>();
    public DbSet<ExecutionTransitionEntity> ExecutionTransitions => Set<ExecutionTransitionEntity>();
    public DbSet<InstanceVariableEntity> InstanceVariables => Set<InstanceVariableEntity>();
    public DbSet<EnvironmentVariableEntity> EnvironmentVariables => Set<EnvironmentVariableEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Runtime");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RuntimeStorageDbContext).Assembly);
        modelBuilder.Entity<DurableAction>()
            .HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_MuleActions_CorrelationId");
        base.OnModelCreating(modelBuilder);
    }

    internal IDisposable DeferAutoSave()
    {
        _deferAutoSaveDepth++;
        return new AutoSaveScope(this);
    }

    private sealed class AutoSaveScope : IDisposable
    {
        private RuntimeStorageDbContext _dbContext;

        public AutoSaveScope(RuntimeStorageDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Dispose()
        {
            var dbContext = _dbContext;
            if (dbContext is null)
                return;

            _dbContext = null!;
            dbContext._deferAutoSaveDepth = Math.Max(0, dbContext._deferAutoSaveDepth - 1);
        }
    }
}
