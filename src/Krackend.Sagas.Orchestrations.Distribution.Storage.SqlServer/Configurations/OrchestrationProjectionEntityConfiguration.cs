using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class OrchestrationProjectionEntityConfiguration : IEntityTypeConfiguration<OrchestrationProjectionEntity>
{
    public void Configure(EntityTypeBuilder<OrchestrationProjectionEntity> builder)
    {
        builder.ToTable("Orchestrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired(false);
        builder.HasIndex(x => x.Key);
    }
}

