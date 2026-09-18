using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class ReleasePlanTargetEntityConfiguration : IEntityTypeConfiguration<ReleasePlanTargetEntity>
{
    public void Configure(EntityTypeBuilder<ReleasePlanTargetEntity> builder)
    {
        builder.ToTable("ReleasePlanTargets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ReleaseId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeNodeId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Notes).HasMaxLength(2048).IsRequired(false);

        builder.HasOne(x => x.Release)
            .WithMany(x => x.Targets)
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RuntimeNode)
            .WithMany()
            .HasForeignKey(x => x.RuntimeNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ReleaseId, x.RuntimeNodeId }).IsUnique();
    }
}


