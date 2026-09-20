using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class DistributionEnvironmentEntityConfiguration : IEntityTypeConfiguration<DistributionEnvironmentEntity>
{
    public void Configure(EntityTypeBuilder<DistributionEnvironmentEntity> builder)
    {
        builder.ToTable("Environments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired(false);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
