using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Configurations;

/// <summary>
/// Represents ParallelGroupDefinitionEntityConfiguration.
/// </summary>
internal sealed class ParallelGroupDefinitionEntityConfiguration : IEntityTypeConfiguration<ParallelGroupDefinitionEntity>
{
    /// <summary>
    /// Configures the entity mapping.
    /// </summary>
    public void Configure(EntityTypeBuilder<ParallelGroupDefinitionEntity> builder)
    {
        builder.ToTable("ParallelGroupDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.StageDefinitionId).HasConversion(new IdToBytesConverter());
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.JoinPolicy).HasConversion<string>().HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.StageDefinitionId);
    }
}
