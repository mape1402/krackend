using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Configurations;

/// <summary>
/// Configures team entity mapping.
/// </summary>
internal sealed class TeamEntityConfiguration : IEntityTypeConfiguration<TeamEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamEntity> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.DisplayName);
    }
}
