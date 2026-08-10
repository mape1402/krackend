using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class OrchestrationAllowedRuntimeNodeEntityConfiguration : IEntityTypeConfiguration<OrchestrationAllowedRuntimeNodeEntity>
{
    public void Configure(EntityTypeBuilder<OrchestrationAllowedRuntimeNodeEntity> builder)
    {
        builder.ToTable("OrchestrationAllowedRuntimeNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RuntimeNodeId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasOne(x => x.Orchestration)
            .WithMany(x => x.AllowedRuntimeNodes)
            .HasForeignKey(x => x.OrchestrationDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RuntimeNode)
            .WithMany()
            .HasForeignKey(x => x.RuntimeNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrchestrationDefinitionId, x.RuntimeNodeId }).IsUnique();
    }
}
