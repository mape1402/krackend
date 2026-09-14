using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Configurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Represents the transactional Entity Framework context used by the orchestration control plane.
/// </summary>
public sealed class ControlPlaneDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlPlaneDbContext"/> class.
    /// </summary>
    /// <param name="options">Entity Framework options for the context.</param>
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets orchestration definitions.
    /// </summary>
    public DbSet<OrchestrationDefinitionEntity> OrchestrationDefinitions => Set<OrchestrationDefinitionEntity>();

    /// <summary>
    /// Gets design domains.
    /// </summary>
    public DbSet<DomainEntity> Domains => Set<DomainEntity>();

    /// <summary>
    /// Gets orchestration versions.
    /// </summary>
    public DbSet<OrchestrationVersionEntity> OrchestrationVersions => Set<OrchestrationVersionEntity>();

    /// <summary>
    /// Gets stage definitions.
    /// </summary>
    public DbSet<StageDefinitionEntity> StageDefinitions => Set<StageDefinitionEntity>();

    /// <summary>
    /// Gets task definitions.
    /// </summary>
    public DbSet<TaskDefinitionEntity> TaskDefinitions => Set<TaskDefinitionEntity>();

    /// <summary>
    /// Gets trigger bindings.
    /// </summary>
    public DbSet<TriggerBindingEntity> TriggerBindings => Set<TriggerBindingEntity>();

    /// <summary>
    /// Gets variable definitions.
    /// </summary>
    public DbSet<VariableDefinitionEntity> VariableDefinitions => Set<VariableDefinitionEntity>();

    /// <summary>
    /// Gets parallel group definitions.
    /// </summary>
    public DbSet<ParallelGroupDefinitionEntity> ParallelGroupDefinitions => Set<ParallelGroupDefinitionEntity>();

    /// <summary>
    /// Gets branch rule definitions.
    /// </summary>
    public DbSet<BranchRuleDefinitionEntity> BranchRuleDefinitions => Set<BranchRuleDefinitionEntity>();

    /// <summary>
    /// Gets runtime nodes.
    /// </summary>
    public DbSet<RuntimeNodeEntity> RuntimeNodes => Set<RuntimeNodeEntity>();

    /// <summary>
    /// Gets runtime capabilities.
    /// </summary>
    public DbSet<RuntimeCapabilityEntity> RuntimeCapabilities => Set<RuntimeCapabilityEntity>();

    /// <summary>
    /// Gets runtime node allow-list rows by orchestration.
    /// </summary>
    public DbSet<OrchestrationAllowedRuntimeNodeEntity> OrchestrationAllowedRuntimeNodes => Set<OrchestrationAllowedRuntimeNodeEntity>();

    /// <summary>
    /// Gets artifact records generated from orchestration versions.
    /// </summary>
    public DbSet<ArtifactEntity> Artifacts => Set<ArtifactEntity>();

    /// <summary>
    /// Gets release records.
    /// </summary>
    public DbSet<ReleaseEntity> Releases => Set<ReleaseEntity>();

    /// <summary>
    /// Gets planned release targets.
    /// </summary>
    public DbSet<ReleasePlanTargetEntity> ReleasePlanTargets => Set<ReleasePlanTargetEntity>();

    /// <summary>
    /// Gets release targets.
    /// </summary>
    public DbSet<ReleaseTargetEntity> ReleaseTargets => Set<ReleaseTargetEntity>();

    /// <summary>
    /// Gets release attempts.
    /// </summary>
    public DbSet<ReleaseAttemptEntity> ReleaseAttempts => Set<ReleaseAttemptEntity>();

    /// <summary>
    /// Gets teams.
    /// </summary>
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    /// <summary>
    /// Gets team members.
    /// </summary>
    public DbSet<TeamMemberEntity> TeamMembers => Set<TeamMemberEntity>();

    /// <summary>
    /// Saves all pending changes after normalizing polymorphic JSON discriminators.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        NormalizePolymorphicDiscriminators();
        return base.SaveChanges();
    }

    /// <summary>
    /// Saves all pending changes after normalizing polymorphic JSON discriminators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizePolymorphicDiscriminators();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Configures control-plane mappings.
    /// </summary>
    /// <param name="modelBuilder">Model builder used by Entity Framework.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BranchRuleDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DomainEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrchestrationDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrchestrationVersionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ParallelGroupDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StageDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TaskDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TriggerBindingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VariableDefinitionEntityConfiguration());

        modelBuilder.ApplyConfiguration(new ArtifactEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ReleaseTargetEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ReleaseAttemptEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrchestrationAllowedRuntimeNodeEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ReleaseEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ReleasePlanTargetEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RuntimeCapabilityEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RuntimeNodeEntityConfiguration());

        modelBuilder.ApplyConfiguration(new TeamEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TeamMemberEntityConfiguration());

        ApplySchema(modelBuilder, ".Design.", "Design");
        ApplySchema(modelBuilder, ".Distribution.", "Distribution");
        ApplySchema(modelBuilder, ".Security.", "Security");

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplySchema(ModelBuilder modelBuilder, string namespaceSegment, string schema)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.Namespace?.Contains(namespaceSegment, StringComparison.Ordinal) == true)
            {
                entityType.SetSchema(schema);
            }
        }
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
            EnsureTaskValidationConfigurationTypes(configuration);
            return;
        }

        configuration.Type = kind switch
        {
            TaskKind.Http => "http",
            TaskKind.Messaging => "messaging",
            TaskKind.Plugin => "plugin",
            _ => "humanApproval",
        };

        EnsureTaskValidationConfigurationTypes(configuration);
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

    private static void EnsureValidationConfigurationType(ValidationConfigurationEnvelopeJsonModel configuration)
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
            EnsureTriggerValidationConfigurationTypes(triggerChannel);
            return;
        }

        triggerChannel.Type = "event";
        EnsureTriggerValidationConfigurationTypes(triggerChannel);
    }

    private static void EnsureTaskValidationConfigurationTypes(TaskConfigurationEnvelopeJsonModel configuration)
    {
        EnsureValidationConfigurationType(configuration?.Messaging?.RequestValidation?.Configuration);
        EnsureValidationConfigurationType(configuration?.Messaging?.ResponseValidation?.Configuration);
    }

    private static void EnsureTriggerValidationConfigurationTypes(TriggerChannelEnvelopeJsonModel triggerChannel)
    {
        EnsureValidationConfigurationType(triggerChannel?.Event?.Validation?.Configuration);
    }
}
