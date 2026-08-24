using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents OrchestrationVersionEntityConfiguration.
/// </summary>
internal sealed class OrchestrationVersionEntityConfiguration : IEntityTypeConfiguration<OrchestrationVersionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<OrchestrationVersionEntity> builder)
    {
        builder.ToTable("OrchestrationVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.VersionLabel).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.Checksum).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ApprovedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.UpdatedBy).HasMaxLength(128).IsRequired(false);

        builder.HasIndex(x => new { x.OrchestrationDefinitionId, x.Version }).IsUnique();
        builder.HasIndex(x => x.OrchestrationDefinitionId);
    }
}
