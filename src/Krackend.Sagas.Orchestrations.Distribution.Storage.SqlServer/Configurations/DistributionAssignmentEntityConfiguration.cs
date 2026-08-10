using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class ReleaseTargetEntityConfiguration : IEntityTypeConfiguration<ReleaseTargetEntity>
{
    public void Configure(EntityTypeBuilder<ReleaseTargetEntity> builder)
    {
        builder.ToTable("ReleaseTargets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeNodeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ReleaseId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RolloutGroup).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.FailureReason).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.RuntimeVersionApplied).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired(false);

        builder.HasOne(x => x.RuntimeNode).WithMany().HasForeignKey(x => x.RuntimeNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Artifact).WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Release).WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.SetNull);
    }
}


