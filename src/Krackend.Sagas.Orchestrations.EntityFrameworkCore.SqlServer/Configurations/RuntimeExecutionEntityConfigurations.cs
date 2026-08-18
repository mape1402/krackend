using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Configurations;

internal sealed class TriggerIntakeAttemptEntityConfiguration : IEntityTypeConfiguration<TriggerIntakeAttemptEntity>
{
    public void Configure(EntityTypeBuilder<TriggerIntakeAttemptEntity> builder)
    {
        builder.ToTable("TriggerIntakeAttempts");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.TriggerIntakeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ActionType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.TriggerIntakeId, x.AttemptNumber });
    }
}

internal sealed class OrchestrationInstanceEntityConfiguration : IEntityTypeConfiguration<OrchestrationInstanceEntity>
{
    public void Configure(EntityTypeBuilder<OrchestrationInstanceEntity> builder)
    {
        builder.ToTable("OrchestrationInstances");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.EnvironmentKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RuntimeOrchestrationArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TriggerIntakeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExecutionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.CurrentStageKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentTaskKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CurrentParallelGroupKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FinalOutcome).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.ActiveLeaseId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.SnapshotPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.EnvironmentKey, x.Status });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.ExecutionKey).IsUnique();
    }
}

internal sealed class StageExecutionEntityConfiguration : IEntityTypeConfiguration<StageExecutionEntity>
{
    public void Configure(EntityTypeBuilder<StageExecutionEntity> builder)
    {
        builder.ToTable("StageExecutions");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.ErrorSummary).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.StageKey }).IsUnique();
    }
}

internal sealed class TaskExecutionEntityConfiguration : IEntityTypeConfiguration<TaskExecutionEntity>
{
    public void Configure(EntityTypeBuilder<TaskExecutionEntity> builder)
    {
        builder.ToTable("TaskExecutions");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TaskKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ParallelGroupId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.SkipReason).HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.OnErrorPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.OutputVariablesPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.TaskKey });
        builder.HasIndex(x => new { x.StageExecutionId, x.TaskKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.WaitingSinceUtc });
        builder.HasIndex(x => x.CorrelationId);
    }
}

internal sealed class TaskExecutionAttemptEntityConfiguration : IEntityTypeConfiguration<TaskExecutionAttemptEntity>
{
    public void Configure(EntityTypeBuilder<TaskExecutionAttemptEntity> builder)
    {
        builder.ToTable("TaskExecutionAttempts");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.TaskExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ResponsePayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ErrorCode).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.DispatchId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.TaskExecutionId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.DispatchId);
        builder.HasIndex(x => new { x.Status, x.WaitingSinceUtc });
    }
}

internal sealed class TaskDispatchEntityConfiguration : IEntityTypeConfiguration<TaskDispatchEntity>
{
    public void Configure(EntityTypeBuilder<TaskDispatchEntity> builder)
    {
        builder.ToTable("TaskDispatches");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.TaskExecutionAttemptId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.DispatchType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.RequestPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.DispatchStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CommandId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.FailureReason).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => x.CommandId);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.DispatchStatus, x.ScheduledOnUtc });
    }
}

internal sealed class CompensationExecutionEntityConfiguration : IEntityTypeConfiguration<CompensationExecutionEntity>
{
    public void Configure(EntityTypeBuilder<CompensationExecutionEntity> builder)
    {
        builder.ToTable("CompensationExecutions");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.SourceTaskExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CompensationTaskKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ResponsePayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.SourceTaskExecutionId });
    }
}

internal sealed class ExecutionTransitionEntityConfiguration : IEntityTypeConfiguration<ExecutionTransitionEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionTransitionEntity> builder)
    {
        builder.ToTable("ExecutionTransitions");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TaskExecutionAttemptId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TransitionType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ToStatus).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Message).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.ProducedBy).HasMaxLength(256).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.OccurredOnUtc });
    }
}

internal sealed class InstanceVariableEntityConfiguration : IEntityTypeConfiguration<InstanceVariableEntity>
{
    public void Configure(EntityTypeBuilder<InstanceVariableEntity> builder)
    {
        builder.ToTable("InstanceVariables");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.OrchestrationInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.SourceType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.LastUpdatedBy).HasMaxLength(256).IsRequired(false);
        builder.HasIndex(x => new { x.OrchestrationInstanceId, x.Key }).IsUnique();
    }
}

internal sealed class EnvironmentVariableEntityConfiguration : IEntityTypeConfiguration<EnvironmentVariableEntity>
{
    public void Configure(EntityTypeBuilder<EnvironmentVariableEntity> builder)
    {
        builder.ToTable("EnvironmentVariables");
        builder.HasKey(x => x.Id);
        RuntimeEntityConfiguration.ConfigureId(builder);
        builder.Property(x => x.EnvironmentKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.VariableKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(1024).IsRequired(false);
        builder.HasIndex(x => new { x.EnvironmentKey, x.VariableKey }).IsUnique();
    }
}

internal static class RuntimeEntityConfiguration
{
    public static void ConfigureId<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
        => builder.Property("Id").HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
}
