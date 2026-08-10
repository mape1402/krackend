using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class ReleasePlanTargetEntityConfiguration : IEntityTypeConfiguration<ReleasePlanTargetEntity>
{
    public void Configure(EntityTypeBuilder<ReleasePlanTargetEntity> builder)
    {
        builder.ToTable("ReleasePlanTargets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ReleaseId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeNodeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
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


