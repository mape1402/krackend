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
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.InboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.InboundSecretHash).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.InboundAllowedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.InboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.InboundLastFailureReason).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.OutboundClientId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.OutboundKeyId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.ProtectedOutboundSecret).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.OutboundRequestedScopes).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.OutboundCredentialStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.InboundClientId).IsUnique().HasFilter("[IsDeleted] = 0 AND [InboundClientId] IS NOT NULL AND [InboundClientId] <> ''");
        builder.HasIndex(x => x.IsDeleted);

        builder.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

