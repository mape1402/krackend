using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

public sealed class RuntimeStorageDbContext : DbContext
{
    public RuntimeStorageDbContext(DbContextOptions<RuntimeStorageDbContext> options)
        : base(options)
    {
    }

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
        base.OnModelCreating(modelBuilder);
    }
}
