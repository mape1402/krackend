using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents StageDefinitionEntityConfiguration.
/// </summary>
internal sealed class StageDefinitionEntityConfiguration : IEntityTypeConfiguration<StageDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<StageDefinitionEntity> builder)
    {
        builder.ToTable("StageDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationVersionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);

        builder.Property(x => x.ExecutionCondition)
            .HasColumnName("ExecutionConditionJson")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<ExecutionConditionJsonModel>(value, (JsonSerializerOptions)null));
        builder.HasIndex(x => x.OrchestrationVersionId);
        builder.HasIndex(x => new { x.OrchestrationVersionId, x.Order });
    }
}
