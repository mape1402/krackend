using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class OrchestrationAllowedRuntimeNodeEntityConfiguration : IEntityTypeConfiguration<OrchestrationAllowedRuntimeNodeEntity>
{
    public void Configure(EntityTypeBuilder<OrchestrationAllowedRuntimeNodeEntity> builder)
    {
        builder.ToTable("OrchestrationAllowedRuntimeNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RuntimeNodeId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasOne(x => x.RuntimeNode)
            .WithMany()
            .HasForeignKey(x => x.RuntimeNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrchestrationDefinitionId, x.RuntimeNodeId }).IsUnique();
    }
}
