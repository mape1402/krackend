using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class ReleaseAttemptEntityConfiguration : IEntityTypeConfiguration<ReleaseAttemptEntity>
{
    public void Configure(EntityTypeBuilder<ReleaseAttemptEntity> builder)
    {
        builder.ToTable("ReleaseAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ReleaseTargetId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.InitiatedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.ExternalReference).HasMaxLength(256).IsRequired(false);
        builder.HasOne(x => x.ReleaseTarget).WithMany(x => x.Attempts).HasForeignKey(x => x.ReleaseTargetId).OnDelete(DeleteBehavior.Cascade);
    }
}


