using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Configurations;

/// <summary>
/// Configures persistence mapping for domain catalog entries.
/// </summary>
internal sealed class DomainEntityConfiguration : IEntityTypeConfiguration<DomainEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    /// <param name="builder">Entity type builder.</param>
    public void Configure(EntityTypeBuilder<DomainEntity> builder)
    {
        builder.ToTable("Domains");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);

        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.DisplayName);
        builder.HasIndex(x => x.Description);
    }
}
