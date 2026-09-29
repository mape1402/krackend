using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Configures persistence mapping for orchestration metadata descriptors.
/// </summary>
internal sealed class MetadataDescriptorEntityConfiguration : IEntityTypeConfiguration<MetadataDescriptorEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    /// <param name="builder">Entity type builder.</param>
    public void Configure(EntityTypeBuilder<MetadataDescriptorEntity> builder)
    {
        builder.ToTable("MetadataDescriptors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.SchemaJson).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.SourceKey);
        builder.HasIndex(x => x.DisplayName);
        builder.HasIndex(x => x.ContentHash);
    }
}
