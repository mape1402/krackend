using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.EntityFrameworkCore;
using Mule.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

public sealed class RuntimeDbContext : DbContext
{
    public RuntimeDbContext(DbContextOptions<RuntimeDbContext> options)
        : base(options)
    {
    }

    public DbSet<RuntimeOrchestrationArtifact> RuntimeOrchestrationArtifacts => Set<RuntimeOrchestrationArtifact>();

    public DbSet<RuntimeDesignNode> RuntimeDesignNodes => Set<RuntimeDesignNode>();

    public DbSet<OrchestrationInstance> OrchestrationInstances => Set<OrchestrationInstance>();

    public DbSet<StageExecution> StageExecutions => Set<StageExecution>();

    public DbSet<TaskExecution> TaskExecutions => Set<TaskExecution>();

    public DbSet<TaskExecutionAttempt> TaskExecutionAttempts => Set<TaskExecutionAttempt>();

    public DbSet<TaskDispatch> TaskDispatches => Set<TaskDispatch>();

    public DbSet<ExecutionTransition> ExecutionTransitions => Set<ExecutionTransition>();

    public DbSet<InstanceVariable> InstanceVariables => Set<InstanceVariable>();

    public DbSet<EnvironmentVariableValue> EnvironmentVariableValues => Set<EnvironmentVariableValue>();

    public DbSet<CompensationExecution> CompensationExecutions => Set<CompensationExecution>();

    public DbSet<RuntimeIngressConfiguration> RuntimeIngressConfigurations => Set<RuntimeIngressConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Runtime");
        ConfigureRuntimeArtifacts(modelBuilder);
        ConfigureRuntimeDesignNodes(modelBuilder);
        ConfigureInstances(modelBuilder);
        ConfigureStages(modelBuilder);
        ConfigureTasks(modelBuilder);
        ConfigureAttempts(modelBuilder);
        ConfigureDispatches(modelBuilder);
        ConfigureTransitions(modelBuilder);
        ConfigureVariables(modelBuilder);
        ConfigureCompensations(modelBuilder);
        ConfigureIngressConfigurations(modelBuilder);
        modelBuilder.UseMuleModel();
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureRuntimeDesignNodes(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeDesignNode>();
        builder.ToTable("RuntimeDesignNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EndpointBaseUri).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.RemoteRuntimeNodeId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DistributionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.InboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundSecretHash).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.InboundAllowedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.InboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.InboundLastFailureReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OutboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.OutboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ProtectedOutboundSecret).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.OutboundRequestedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.OutboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasDefaultValue(RuntimeDesignNodeStatus.Pending)
            .HasSentinel(RuntimeDesignNodeStatus.Pending)
            .IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.InboundClientId).IsUnique().HasFilter("[InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsEnabled);
    }

    private static void ConfigureRuntimeArtifacts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeOrchestrationArtifact>();
        builder.ToTable("RuntimeOrchestrationArtifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ArtifactType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceOrchestrationVersionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Version).HasMaxLength(64).HasConversion(new SemanticVersionConverter()).IsRequired();
        builder.Property(x => x.ArtifactChecksum).HasMaxLength(256).HasConversion(new ChecksumConverter()).IsRequired();
        builder.Property(x => x.ArtifactPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).HasDefaultValue(RuntimeOrchestrationArtifactStatus.Pending).IsRequired();
        builder.Property(x => x.ProjectionError).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.SupersededByArtifactId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationDefinitionKey, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.OrchestrationDefinitionKey, x.IsActive });
        builder.HasIndex(x => new { x.Status, x.IsActive });
    }

    private static void ConfigureInstances(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<OrchestrationInstance>();
        builder.ToTable("OrchestrationInstances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TriggerIntakeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExecutionKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.CurrentStageKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentTaskKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentParallelGroupKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FinalOutcome).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.ActiveLeaseId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.SnapshotPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.LastUpdatedOnUtc);
        builder.HasIndex(x => x.CorrelationId);
    }

    private static void ConfigureStages(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<StageExecution>();
        builder.ToTable("StageExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.StageKey }).IsUnique();
    }

    private static void ConfigureTasks(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskExecution>();
        builder.ToTable("TaskExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ParallelGroupId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.TaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TaskKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.OnErrorPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OutputVariablesPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.StageExecutionId, x.TaskKey }).IsUnique();
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.Status);
    }

    private static void ConfigureAttempts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskExecutionAttempt>();
        builder.ToTable("TaskExecutionAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.DispatchId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ResponsePayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ErrorCode).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => new { x.TaskExecutionId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.DispatchId);
        builder.HasIndex(x => x.Status);
    }

    private static void ConfigureDispatches(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TaskDispatch>();
        builder.ToTable("TaskDispatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionAttemptId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.DispatchType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.RequestPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.DispatchStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CommandId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FailureReason).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.TaskExecutionAttemptId).IsUnique();
        builder.HasIndex(x => x.CommandId);
        builder.HasIndex(x => x.DispatchStatus);
    }

    private static void ConfigureTransitions(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ExecutionTransition>();
        builder.ToTable("ExecutionTransitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.TaskExecutionId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.TaskExecutionAttemptId).HasColumnType("binary(16)").HasConversion(new NullableIdToBytesConverter());
        builder.Property(x => x.TransitionType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ToStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ProducedBy).HasMaxLength(256).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.OccurredOnUtc });
        builder.HasIndex(x => x.OccurredOnUtc);
    }

    private static void ConfigureVariables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstanceVariable>().ToTable("InstanceVariables").HasKey(x => x.Id);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<InstanceVariable>().Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Key).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Scope).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.Value).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.SourceType).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<InstanceVariable>().Property(x => x.SourceReference).HasMaxLength(512).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().Property(x => x.LastUpdatedBy).HasMaxLength(128).IsRequired(false);
        modelBuilder.Entity<InstanceVariable>().HasIndex(x => new { x.OrchestrationInstanceId, x.Key }).IsUnique();

        modelBuilder.Entity<EnvironmentVariableValue>().ToTable("EnvironmentVariableValues").HasKey(x => x.Id);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.VariableKey).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Value).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.UpdatedBy).HasMaxLength(128).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        modelBuilder.Entity<EnvironmentVariableValue>().HasIndex(x => x.VariableKey).IsUnique();
    }

    private static void ConfigureCompensations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CompensationExecution>();
        builder.ToTable("CompensationExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.SourceTaskExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CompensationTaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ResponsePayload).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeConverter()).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").HasConversion(new JsonNodeDictionaryConverter(), new JsonNodeDictionaryComparer()).IsRequired(false);
        builder.HasIndex(x => x.OrchestrationInstanceId);
    }

    private static void ConfigureIngressConfigurations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeIngressConfiguration>();
        builder.ToTable("RuntimeIngressConfigurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ConfigurationKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IngressKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.IngressTransport).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SettingsPayload).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(x => new { x.RuntimeOrchestrationArtifactId, x.ConfigurationKey }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.IngressTransport, x.IngressKind });
        builder.HasIndex(x => new { x.IsActive, x.RuntimeOrchestrationArtifactId });
    }
}
