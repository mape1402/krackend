using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Configurations;

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

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageDefinitionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.FromType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.FromId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.NavigateToType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.NavigateToId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());

        builder.Property(x => x.Condition)
            .HasColumnName("ConditionJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<ExecutionConditionJsonModel>(value, (JsonSerializerOptions)null));

        builder.HasIndex(x => x.StageDefinitionId);
    }
}
