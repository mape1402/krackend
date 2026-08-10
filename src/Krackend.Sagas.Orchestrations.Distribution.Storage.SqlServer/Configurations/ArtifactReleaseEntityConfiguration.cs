using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Configurations;

internal sealed class ArtifactEntityConfiguration : IEntityTypeConfiguration<ArtifactEntity>
{
    public void Configure(EntityTypeBuilder<ArtifactEntity> builder)
    {
        builder.ToTable("Artifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationDefinitionId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OrchestrationVersionId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OrchestrationDisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.VersionLabel).HasMaxLength(128).IsRequired(false);
        builder.Property(x => x.VersionNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ArtifactType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Metadata).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.SourceEvent).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SourceVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.OrchestrationVersionId, x.ArtifactType }).IsUnique();
    }
}


