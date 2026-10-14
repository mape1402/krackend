using Krackend.EventSourcing.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Krackend.EventSourcing.EntityFrameworkCore;

/// <summary>
/// Configures Entity Framework Core-specific event store mapping behavior.
/// </summary>
public sealed class EntityFrameworkEventStoreOptions
{
    private readonly EntityFrameworkEventStoreKeyOptions _keys = new();
    private readonly EntityFrameworkEventStoreEntityOptions _events = new();
    private readonly List<Action<EntityTypeBuilder<EventSnapshotRecord>>> _snapshotConfigurators = [];
    private readonly List<Action<EntityTypeBuilder<SnapshotCandidateRecord>>> _snapshotCandidateConfigurators = [];

    /// <summary>
    /// Configures the primary key mapping used by event store tables.
    /// </summary>
    /// <param name="configure">The key mapping configuration callback.</param>
    public void Keys(Action<EntityFrameworkEventStoreKeyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(_keys);
    }

    /// <summary>
    /// Configures persisted event table mappings.
    /// </summary>
    /// <param name="configure">The event table mapping configuration callback.</param>
    public void Events(Action<EntityFrameworkEventStoreEntityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(_events);
    }

    /// <summary>
    /// Configures the snapshot table mapping.
    /// </summary>
    /// <param name="configure">The snapshot entity mapping configuration callback.</param>
    public void Snapshots(Action<EntityTypeBuilder<EventSnapshotRecord>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _snapshotConfigurators.Add(configure);
    }

    /// <summary>
    /// Configures the snapshot candidate table mapping.
    /// </summary>
    /// <param name="configure">The snapshot candidate entity mapping configuration callback.</param>
    public void SnapshotCandidates(Action<EntityTypeBuilder<SnapshotCandidateRecord>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _snapshotCandidateConfigurators.Add(configure);
    }

    internal void ConfigureKey(PropertyBuilder<string> propertyBuilder)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        propertyBuilder.HasMaxLength(26);
        _keys.Apply(propertyBuilder);
    }

    internal void ConfigureEventEntity(
        EntityTypeBuilder<EventStoreRecord> entity,
        EventStoreOptions store)
        => _events.Apply(entity, store);

    internal void ConfigureSnapshotEntity(EntityTypeBuilder<EventSnapshotRecord> entity)
    {
        foreach (var configure in _snapshotConfigurators)
            configure(entity);
    }

    internal void ConfigureSnapshotCandidateEntity(EntityTypeBuilder<SnapshotCandidateRecord> entity)
    {
        foreach (var configure in _snapshotCandidateConfigurators)
            configure(entity);
    }

    internal int GetServiceProviderHashCode()
    {
        var hashCode = new HashCode();
        _keys.AddToHashCode(ref hashCode);
        _events.AddToHashCode(ref hashCode);

        foreach (var configure in _snapshotConfigurators)
            hashCode.Add(configure);

        foreach (var configure in _snapshotCandidateConfigurators)
            hashCode.Add(configure);

        return hashCode.ToHashCode();
    }
}

/// <summary>
/// Configures primary key storage for Krackend Event Sourcing EF Core tables.
/// </summary>
public sealed class EntityFrameworkEventStoreKeyOptions
{
    private readonly List<Action<PropertyBuilder<string>>> _configurators = [];

    /// <summary>
    /// Adds custom EF Core mapping for every Krackend Event Sourcing primary key.
    /// </summary>
    /// <param name="configure">The property mapping callback.</param>
    public void Configure(Action<PropertyBuilder<string>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _configurators.Add(configure);
    }

    /// <summary>
    /// Stores ULID string keys as 26-character text values.
    /// </summary>
    /// <param name="maxLength">The maximum persisted key length.</param>
    public void UseUlidStrings(int maxLength = 26)
    {
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Maximum length must be greater than zero.");

        Configure(property => property.HasMaxLength(maxLength));
    }

    /// <summary>
    /// Stores ULID string keys as compact 16-byte values.
    /// </summary>
    /// <param name="columnType">Optional provider-specific column type.</param>
    public void UseUlidBytes(string? columnType = null)
        => Configure(property => property.HasUlidBytesConversion(columnType).HasMaxLength(16));

    internal void Apply(PropertyBuilder<string> propertyBuilder)
    {
        foreach (var configure in _configurators)
            configure(propertyBuilder);
    }

    internal void AddToHashCode(ref HashCode hashCode)
    {
        foreach (var configure in _configurators)
            hashCode.Add(configure);
    }
}

/// <summary>
/// Configures persisted event entity mappings.
/// </summary>
public sealed class EntityFrameworkEventStoreEntityOptions
{
    private readonly List<Action<EntityTypeBuilder<EventStoreRecord>, EventStoreOptions>> _globalConfigurators = [];
    private readonly Dictionary<string, List<Action<EntityTypeBuilder<EventStoreRecord>, EventStoreOptions>>> _storeConfigurators = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds mapping configuration to every event store table.
    /// </summary>
    /// <param name="configure">The entity mapping callback.</param>
    public void ForAll(Action<EntityTypeBuilder<EventStoreRecord>, EventStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _globalConfigurators.Add(configure);
    }

    /// <summary>
    /// Adds mapping configuration to a specific event store table.
    /// </summary>
    /// <param name="storeName">The configured event store name.</param>
    /// <param name="configure">The entity mapping callback.</param>
    public void For(string storeName, Action<EntityTypeBuilder<EventStoreRecord>, EventStoreOptions> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_storeConfigurators.TryGetValue(storeName, out var configurators))
        {
            configurators = [];
            _storeConfigurators[storeName] = configurators;
        }

        configurators.Add(configure);
    }

    internal void Apply(EntityTypeBuilder<EventStoreRecord> entity, EventStoreOptions store)
    {
        foreach (var configure in _globalConfigurators)
            configure(entity, store);

        if (!_storeConfigurators.TryGetValue(store.Name, out var storeConfigurators))
            return;

        foreach (var configure in storeConfigurators)
            configure(entity, store);
    }

    internal void AddToHashCode(ref HashCode hashCode)
    {
        foreach (var configure in _globalConfigurators)
            hashCode.Add(configure);

        foreach (var pair in _storeConfigurators.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hashCode.Add(pair.Key, StringComparer.Ordinal);

            foreach (var configure in pair.Value)
                hashCode.Add(configure);
        }
    }
}
