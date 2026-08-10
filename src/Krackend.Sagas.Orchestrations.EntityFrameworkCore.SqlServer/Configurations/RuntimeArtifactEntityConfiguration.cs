using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Configurations;

internal sealed class RuntimeArtifactEntityConfiguration : IEntityTypeConfiguration<RuntimeArtifactEntity>
{
    public void Configure(EntityTypeBuilder<RuntimeArtifactEntity> builder)
    {
        builder.ToTable("Artifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.EnvironmentKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OrchestrationDefinitionKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ArtifactType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceOrchestrationVersionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ArtifactChecksum).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ArtifactPayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.SupersededByArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.HasIndex(x => new { x.EnvironmentKey, x.OrchestrationDefinitionKey, x.Version, x.ArtifactType }).IsUnique();
        builder.HasIndex(x => new { x.EnvironmentKey, x.OrchestrationDefinitionKey, x.IsActive });
    }
}
