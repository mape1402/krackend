using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Configurations;

internal sealed class TriggerIntakeEntityConfiguration : IEntityTypeConfiguration<TriggerIntakeEntity>
{
    public void Configure(EntityTypeBuilder<TriggerIntakeEntity> builder)
    {
        builder.ToTable("TriggerIntakes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.TriggerType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggerKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EnvironmentKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.SourceMessageId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.SourceRequestId).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.RawPayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.NormalizedPayloadJson).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.PersistenceLevel).HasMaxLength(64).IsRequired(false);
        builder.Property(x => x.BufferLocation).HasMaxLength(512).IsRequired(false);
        builder.Property(x => x.ResolvedArtifactId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.PromotedInstanceId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.RejectionReason).HasMaxLength(2048).IsRequired(false);
        builder.Property(x => x.FailureReason).HasMaxLength(2048).IsRequired(false);
        builder.HasIndex(x => new { x.EnvironmentKey, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL AND [IdempotencyKey] <> N''")
            .HasDatabaseName("UX_TriggerIntakes_EnvironmentKey_IdempotencyKey");
        builder.HasIndex(x => new { x.EnvironmentKey, x.CorrelationId })
            .HasDatabaseName("IX_TriggerIntakes_EnvironmentKey_CorrelationId");
        builder.HasIndex(x => new { x.EnvironmentKey, x.TriggerKey, x.Status });
    }
}
