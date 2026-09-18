using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Configurations;

/// <summary>
/// Configures team member entity mapping.
/// </summary>
internal sealed class TeamMemberEntityConfiguration : IEntityTypeConfiguration<TeamMemberEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamMemberEntity> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TeamId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.ExternalUserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired(false);

        builder.HasIndex(x => new { x.TeamId, x.ExternalUserId }).IsUnique();
        builder.HasOne(x => x.Team)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
