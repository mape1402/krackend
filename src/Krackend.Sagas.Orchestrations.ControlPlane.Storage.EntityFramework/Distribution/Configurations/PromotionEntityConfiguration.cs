using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class ReleaseEntityConfiguration : IEntityTypeConfiguration<ReleaseEntity>
{
    public void Configure(EntityTypeBuilder<ReleaseEntity> builder)
    {
        builder.ToTable("Releases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Strategy).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.OrchestrationDefinitionId);

        builder.HasOne(x => x.Artifact)
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


