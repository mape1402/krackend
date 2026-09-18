using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents BranchRuleDefinitionEntityConfiguration.
/// </summary>
internal sealed class BranchRuleDefinitionEntityConfiguration : IEntityTypeConfiguration<BranchRuleDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<BranchRuleDefinitionEntity> builder)
    {
        builder.ToTable("BranchRuleDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageDefinitionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.FromType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.FromId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.NavigateToType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.NavigateToId).HasConversion(new IdToBytesConverter());

        builder.Property(x => x.Condition)
            .HasColumnName("ConditionJson")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<ExecutionConditionJsonModel>(value, (JsonSerializerOptions)null));

        builder.HasIndex(x => x.StageDefinitionId);
    }
}
