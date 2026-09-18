using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents VariableDefinitionEntityConfiguration.
/// </summary>
internal sealed class VariableDefinitionEntityConfiguration : IEntityTypeConfiguration<VariableDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<VariableDefinitionEntity> builder)
    {
        builder.ToTable("VariableDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationVersionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.DefaultValue).IsRequired(false);

        builder.HasIndex(x => x.OrchestrationVersionId);
        builder.HasIndex(x => new { x.OrchestrationVersionId, x.Key }).IsUnique();
    }
}
