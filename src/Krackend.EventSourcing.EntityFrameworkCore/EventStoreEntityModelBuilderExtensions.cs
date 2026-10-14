using Krackend.EventSourcing.Configuration;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides EF Core model configuration for Krackend event store entities.
/// </summary>
public static class EventStoreEntityModelBuilderExtensions
{
    private const int NameMaxLength = 128;
    private const int IdentifierMaxLength = 256;
    private const int TypeMaxLength = 256;
    private const int SchemaVersionMaxLength = 32;

    /// <summary>
    /// Adds Krackend event store entity mappings to an EF Core model.
    /// </summary>
    public static ModelBuilder AddKrackendEventStore(
        this ModelBuilder modelBuilder,
        EventStoreOptionsCollection stores,
        Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(stores);

        options ??= new Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions();

        foreach (var store in stores.Values.Values)
        {
            modelBuilder.SharedTypeEntity<Krackend.EventSourcing.EntityFrameworkCore.EventStoreRecord>(
                store.Name,
                entity => ConfigureEntity(entity, store, options));
        }

        modelBuilder.Entity<Krackend.EventSourcing.EntityFrameworkCore.EventSnapshotRecord>(
            entity => ConfigureSnapshotEntity(entity, options));
        modelBuilder.Entity<Krackend.EventSourcing.EntityFrameworkCore.SnapshotCandidateRecord>(
            entity => ConfigureSnapshotCandidateEntity(entity, options));

        return modelBuilder;
    }

    private static void ConfigureEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.EventStoreRecord> entity,
        EventStoreOptions store,
        Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions options)
    {
        entity.ToTable(store.TableName, store.Schema);
        entity.HasKey(x => x.EventId);
        var eventIdProperty = entity.Property(x => x.EventId)
            .IsRequired()
            .ValueGeneratedNever();
        options.ConfigureKey(eventIdProperty);

        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(NameMaxLength);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.StreamType).HasMaxLength(TypeMaxLength);
        entity.Property(x => x.EventType).IsRequired().HasMaxLength(TypeMaxLength);
        entity.Property(x => x.EventSchemaVersion).IsRequired().HasMaxLength(SchemaVersionMaxLength);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.GlobalPosition).IsRequired().ValueGeneratedNever();
        entity.Property(x => x.OccurredAt).IsRequired();
        entity.Property(x => x.CorrelationId).HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.CausationId).HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.UserId).HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.TenantId).HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.Source).HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.Payload).IsRequired();
        entity.Property(x => x.Metadata);
        entity.HasIndex(x => new { x.StreamName, x.StreamId, x.StreamVersion }).IsUnique();
        entity.HasIndex(x => x.GlobalPosition);
        entity.HasIndex(x => new { x.EventType, x.GlobalPosition });
        entity.HasIndex(x => x.CorrelationId);
        entity.HasIndex(x => x.CausationId);
        options.ConfigureEventEntity(entity, store);
    }

    private static void ConfigureSnapshotEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.EventSnapshotRecord> entity,
        Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions options)
    {
        entity.ToTable("EventSnapshots");
        entity.HasKey(x => x.SnapshotId);
        var snapshotIdProperty = entity.Property(x => x.SnapshotId)
            .IsRequired()
            .ValueGeneratedNever();
        options.ConfigureKey(snapshotIdProperty);

        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(NameMaxLength);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.StateType).IsRequired().HasMaxLength(TypeMaxLength);
        entity.Property(x => x.StateSchemaVersion).IsRequired().HasMaxLength(SchemaVersionMaxLength);
        entity.Property(x => x.Payload).IsRequired();
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.HasIndex(x => new { x.StreamName, x.StreamId, x.StreamVersion }).IsUnique();
        options.ConfigureSnapshotEntity(entity);
    }

    private static void ConfigureSnapshotCandidateEntity(
        EntityTypeBuilder<Krackend.EventSourcing.EntityFrameworkCore.SnapshotCandidateRecord> entity,
        Krackend.EventSourcing.EntityFrameworkCore.EntityFrameworkEventStoreOptions options)
    {
        entity.ToTable("EventSnapshotCandidates");
        entity.HasKey(x => x.CandidateId);
        var candidateIdProperty = entity.Property(x => x.CandidateId)
            .IsRequired()
            .ValueGeneratedNever();
        options.ConfigureKey(candidateIdProperty);

        entity.Property(x => x.StreamName).IsRequired().HasMaxLength(NameMaxLength);
        entity.Property(x => x.StreamId).IsRequired().HasMaxLength(IdentifierMaxLength);
        entity.Property(x => x.StreamVersion).IsRequired();
        entity.Property(x => x.MarkedAt).IsRequired();
        entity.HasIndex(x => new { x.StreamName, x.StreamId }).IsUnique();
        entity.HasIndex(x => x.MarkedAt);
        options.ConfigureSnapshotCandidateEntity(entity);
    }
}
