using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;

/// <summary>
/// Represents DesignStorageDbContext.
/// </summary>
public sealed class DesignStorageDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">The options value.</param>
    public DesignStorageDbContext(DbContextOptions<DesignStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrchestrationDefinitionEntity> OrchestrationDefinitions => Set<OrchestrationDefinitionEntity>();
    public DbSet<DomainEntity> Domains => Set<DomainEntity>();
    public DbSet<TeamProjectionEntity> TeamProjections => Set<TeamProjectionEntity>();
    public DbSet<OrchestrationVersionEntity> OrchestrationVersions => Set<OrchestrationVersionEntity>();
    public DbSet<StageDefinitionEntity> StageDefinitions => Set<StageDefinitionEntity>();
    public DbSet<TaskDefinitionEntity> TaskDefinitions => Set<TaskDefinitionEntity>();
    public DbSet<TriggerBindingEntity> TriggerBindings => Set<TriggerBindingEntity>();
    public DbSet<VariableDefinitionEntity> VariableDefinitions => Set<VariableDefinitionEntity>();
    public DbSet<ParallelGroupDefinitionEntity> ParallelGroupDefinitions => Set<ParallelGroupDefinitionEntity>();
    public DbSet<BranchRuleDefinitionEntity> BranchRuleDefinitions => Set<BranchRuleDefinitionEntity>();

    /// <summary>
    /// Builds the model metadata for the design storage context.
    /// </summary>
    /// <param name="modelBuilder">Model builder used to configure mappings.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Design");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DesignStorageDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        NormalizePolymorphicDiscriminators();
        return base.SaveChanges();
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizePolymorphicDiscriminators();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void NormalizePolymorphicDiscriminators()
    {
        var trackedStages = ChangeTracker.Entries<StageDefinitionEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);

        foreach (var stage in trackedStages)
        {
            EnsureConditionConfigurationType(stage.ExecutionCondition?.Configuration);
        }

        var trackedBranchRules = ChangeTracker.Entries<BranchRuleDefinitionEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);

        foreach (var branchRule in trackedBranchRules)
        {
            EnsureConditionConfigurationType(branchRule.Condition?.Configuration);
        }

        var trackedTasks = ChangeTracker.Entries<TaskDefinitionEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);

        foreach (var task in trackedTasks)
        {
            EnsureTaskConfigurationType(task.Configuration, task.Kind);
            EnsureConditionConfigurationType(task.ExecutionCondition?.Configuration);
            EnsureTransformationConfigurationType(task.Transformation?.Configuration);
            EnsureRetryStrategyType(task.RetryPolicy?.Strategy);
            EnsureTimeoutBehaviorPolicyType(task.TimeoutPolicy?.TimeoutBehaviorPolicy);

            if (task.CompensationDefinition is not null)
            {
                EnsureTaskConfigurationType(task.CompensationDefinition.Configuration, task.CompensationDefinition.CompensationTaskKind);
                EnsureConditionConfigurationType(task.CompensationDefinition.ExecutionCondition?.Configuration);
                EnsureTransformationConfigurationType(task.CompensationDefinition.Transformation?.Configuration);
                EnsureRetryStrategyType(task.CompensationDefinition.RetryPolicy?.Strategy);
                EnsureTimeoutBehaviorPolicyType(task.CompensationDefinition.TimeoutPolicy?.TimeoutBehaviorPolicy);
            }
        }

        var trackedTriggers = ChangeTracker.Entries<TriggerBindingEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);

        foreach (var trigger in trackedTriggers)
        {
            EnsureTriggerChannelType(trigger.TriggerChannel);
        }
    }

    private static void EnsureTaskConfigurationType(TaskConfigurationEnvelopeJsonModel configuration, TaskKind kind)
    {
        if (configuration is null || !string.IsNullOrWhiteSpace(configuration.Type))
        {
            return;
        }

        configuration.Type = kind switch
        {
            TaskKind.Http => "http",
            TaskKind.Messaging => "messaging",
            TaskKind.Plugin => "plugin",
            _ => "humanApproval",
        };
    }

    private static void EnsureConditionConfigurationType(ConditionConfigurationEnvelopeJsonModel configuration)
    {
        if (configuration is null || !string.IsNullOrWhiteSpace(configuration.Type))
        {
            return;
        }

        configuration.Type = "dsl";
    }

    private static void EnsureTransformationConfigurationType(TransformationConfigurationEnvelopeJsonModel configuration)
    {
        if (configuration is null || !string.IsNullOrWhiteSpace(configuration.Type))
        {
            return;
        }

        configuration.Type = "dsl";
    }

    private static void EnsureRetryStrategyType(RetryStrategyEnvelopeJsonModel strategy)
    {
        if (strategy is null || !string.IsNullOrWhiteSpace(strategy.Type))
        {
            return;
        }

        strategy.Type = "fixed";
    }

    private static void EnsureTimeoutBehaviorPolicyType(TimeoutBehaviorPolicyEnvelopeJsonModel policy)
    {
        if (policy is null || !string.IsNullOrWhiteSpace(policy.Type))
        {
            return;
        }

        policy.Type = policy.Fail is not null
            ? "fail"
            : policy.Wait is not null
                ? "wait"
                : policy.Reconcile is not null
                    ? "reconcile"
                    : "fail";
    }

    private static void EnsureTriggerChannelType(TriggerChannelEnvelopeJsonModel triggerChannel)
    {
        if (triggerChannel is null || !string.IsNullOrWhiteSpace(triggerChannel.Type))
        {
            return;
        }

        triggerChannel.Type = "event";
    }
}
