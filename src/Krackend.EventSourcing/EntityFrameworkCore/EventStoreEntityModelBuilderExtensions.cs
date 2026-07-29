using Krackend.EventSourcing.Configuration;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides EF Core model configuration for Krackend event store entities.
/// </summary>
public static class EventStoreEntityModelBuilderExtensions
{
    /// <summary>
    /// Adds Krackend event store entity mappings to an EF Core model.
    /// </summary>
    public static ModelBuilder AddKrackendEventStore(
        this ModelBuilder modelBuilder,
        EventStoreOptionsCollection stores)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(stores);

        foreach (var store in stores.Values.Values)
        {
            modelBuilder.SharedTypeEntity<Krackend.EventSourcing.EntityFrameworkCore.EventStoreRecord>(
                store.Name,
                entity => ConfigureEntity(entity, store));
        }

        modelBuilder.Entity<Krackend.EventSourcing.EntityFrameworkCore.EventSnapshotRecord>(ConfigureSnapshotEntity);
        modelBuilder.Entity<Krackend.EventSourcing.EntityFrameworkCore.SnapshotCandidateRecord>(ConfigureSnapshotCandidateEntity);

        return modelBuilder;
    }

    private static void ConfigureEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.EventStoreRecord> entity,
        EventStoreOptions store)
    {
        entity.ToTable(store.TableName, store.Schema);
        entity.HasKey(x => x.EventId);
        entity.Property(x => x.EventId).ValueGeneratedNever();
        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(200);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(300);
        entity.Property(x => x.StreamType).HasMaxLength(300);
        entity.Property(x => x.EventType).IsRequired().HasMaxLength(500);
        entity.Property(x => x.EventSchemaVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.GlobalPosition).IsRequired().ValueGeneratedNever();
        entity.Property(x => x.OccurredAt).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(100);
        entity.Property(x => x.CausationId).HasMaxLength(100);
        entity.Property(x => x.UserId).HasMaxLength(200);
        entity.Property(x => x.TenantId).HasMaxLength(200);
        entity.Property(x => x.Source).HasMaxLength(300);
        entity.Property(x => x.Payload).IsRequired();
        entity.Property(x => x.Metadata);
        entity.HasIndex(x => new { x.StreamName, x.StreamId, x.StreamVersion }).IsUnique();
        entity.HasIndex(x => x.GlobalPosition);
        entity.HasIndex(x => new { x.EventType, x.GlobalPosition });
        entity.HasIndex(x => x.CorrelationId);
        entity.HasIndex(x => x.CausationId);
    }

    private static void ConfigureSnapshotEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.EventSnapshotRecord> entity)
    {
        entity.ToTable("EventSnapshots");
        entity.HasKey(x => x.SnapshotId);
        entity.Property(x => x.SnapshotId).ValueGeneratedNever();
        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(200);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(300);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.StateType).IsRequired().HasMaxLength(500);
        entity.Property(x => x.StateSchemaVersion).IsRequired().HasMaxLength(50);
        entity.Property(x => x.Payload).IsRequired();
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.HasIndex(x => new { x.StreamName, x.StreamId, x.StreamVersion }).IsUnique();
    }

    private static void ConfigureSnapshotCandidateEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.SnapshotCandidateRecord> entity)
    {
        entity.ToTable("EventSnapshotCandidates");
        entity.HasKey(x => new { x.StreamName, x.StreamId });
        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(200);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(300);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.MarkedAt).IsRequired();
        entity.HasIndex(x => x.MarkedAt);
    }
}
