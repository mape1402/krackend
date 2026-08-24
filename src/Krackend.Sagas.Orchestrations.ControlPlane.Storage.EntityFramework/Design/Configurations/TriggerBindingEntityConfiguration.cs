using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents TriggerBindingEntityConfiguration.
/// </summary>
internal sealed class TriggerBindingEntityConfiguration : IEntityTypeConfiguration<TriggerBindingEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<TriggerBindingEntity> builder)
    {
        builder.ToTable("TriggerBindings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.OrchestrationVersionId).HasColumnType("binary(16)").HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TriggerType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048).IsRequired(false);

        builder.Property(x => x.TriggerChannel)
            .HasColumnName("TriggerChannelJson")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
                value => JsonSerializer.Deserialize<TriggerChannelEnvelopeJsonModel>(value, (JsonSerializerOptions)null));

        builder.HasIndex(x => x.OrchestrationVersionId);
    }
}
