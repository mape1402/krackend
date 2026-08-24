using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents TaskDefinitionEntityConfiguration.
/// </summary>
internal sealed class TaskDefinitionEntityConfiguration : IEntityTypeConfiguration<TaskDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<TaskDefinitionEntity> builder)
    {
        builder.ToTable("TaskDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageDefinitionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExecutionMode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ParallelGroupId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OnErrorPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.DispatchType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2048).IsRequired(false);

        builder.Property(x => x.ExecutionCondition)
            .HasColumnName("ExecutionConditionJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<ExecutionConditionJsonModel>(value, (JsonSerializerOptions)null));
        builder.Property(x => x.Transformation)
            .HasColumnName("TransformationJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<TransformationDefinitionJsonModel>(value, (JsonSerializerOptions)null));
        builder.Property(x => x.Configuration)
            .HasColumnName("ConfigurationJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<TaskConfigurationEnvelopeJsonModel>(value, (JsonSerializerOptions)null));
        builder.Property(x => x.RetryPolicy)
            .HasColumnName("RetryPolicyJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<RetryPolicyJsonModel>(value, (JsonSerializerOptions)null));
        builder.Property(x => x.TimeoutPolicy)
            .HasColumnName("TimeoutPolicyJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<TimeoutPolicyJsonModel>(value, (JsonSerializerOptions)null));
        builder.Property(x => x.CompensationDefinition)
            .HasColumnName("CompensationDefinitionJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<CompensationDefinitionJsonModel>(value, (JsonSerializerOptions)null));

        builder.HasIndex(x => x.StageDefinitionId);
        builder.HasIndex(x => new { x.StageDefinitionId, x.Order });
    }
}
