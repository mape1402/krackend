using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Configurations;

internal sealed class RuntimeNodeEntityConfiguration : IEntityTypeConfiguration<RuntimeNodeEntity>
{
    public void Configure(EntityTypeBuilder<RuntimeNodeEntity> builder)
    {
        builder.ToTable("RuntimeNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EnvironmentId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.EndpointBaseUri).HasMaxLength(1024).IsRequired(false);
        builder.Property(x => x.EndpointApiPath).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.ClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.SecretReference).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ApiKeyReference).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

