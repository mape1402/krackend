using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Configurations;

/// <summary>
/// Configures persistence mapping for team projections.
/// </summary>
internal sealed class TeamProjectionEntityConfiguration : IEntityTypeConfiguration<TeamProjectionEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamProjectionEntity> builder)
    {
        builder.ToTable("TeamProjections");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.DisplayName);
    }
}
