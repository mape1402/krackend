using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents OrchestrationDefinitionEntityConfiguration.
/// </summary>
internal sealed class OrchestrationDefinitionEntityConfiguration : IEntityTypeConfiguration<OrchestrationDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<OrchestrationDefinitionEntity> builder)
    {
        builder.ToTable("OrchestrationDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Domain).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.DomainId).HasConversion(new IdToBytesConverter()).IsRequired(false);
        builder.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.OwnerTeam).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.OwnerTeamId).HasConversion(new IdToBytesConverter()).IsRequired(false);

        builder.PrimitiveCollection(x => x.Tags);

        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.DomainId);
        builder.HasIndex(x => x.OwnerTeamId);

        builder.HasOne(x => x.DomainRef)
            .WithMany()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.OwnerTeamRef)
            .WithMany()
            .HasForeignKey(x => x.OwnerTeamId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
