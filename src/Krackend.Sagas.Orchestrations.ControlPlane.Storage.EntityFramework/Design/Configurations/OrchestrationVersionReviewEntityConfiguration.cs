namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

/// <summary>
/// Configures durable orchestration version review storage.
/// </summary>
internal sealed class OrchestrationVersionReviewEntityConfiguration : IEntityTypeConfiguration<OrchestrationVersionReviewEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrchestrationVersionReviewEntity> builder)
    {
        builder.ToTable("OrchestrationVersionReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationVersionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Operation).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.PendingStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpectedChecksum).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ApplicationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DecidedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.RejectionComment).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.AbortedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.AbortReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.AppliedBy).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.DeployedArtifactId).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.PendingSlot).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.OrchestrationVersionId);
        builder.HasIndex(x => x.PendingSlot).IsUnique();
    }
}
