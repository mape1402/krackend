using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class RuntimeCapabilityEntityConfiguration : IEntityTypeConfiguration<RuntimeCapabilityEntity>
{
    public void Configure(EntityTypeBuilder<RuntimeCapabilityEntity> builder)
    {
        builder.ToTable("RuntimeCapabilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RuntimeNodeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(512).IsRequired(false);

        builder.HasOne(x => x.RuntimeNode)
            .WithMany(x => x.Capabilities)
            .HasForeignKey(x => x.RuntimeNodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

