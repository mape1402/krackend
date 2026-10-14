using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Tests;

public sealed class EntityFrameworkEventStoreTests
{
    [Fact]
    public void AddKrackendEntityFrameworkEventStore_adds_store_entities_to_app_db_context_model()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.NotNull(dbContext.Model.FindEntityType("orders"));
        Assert.NotNull(dbContext.Model.FindEntityType("payments"));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(EventSnapshotRecord)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord)));
    }

    [Fact]
    public async Task AppendAsync_persists_events_in_app_db_context_event_store()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        var envelopes = await eventStore.ReadStreamAsync("orders", "order-1", fromVersion: 1, maxCount: 10);
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.Single(envelopes);
        Assert.Equal("OrderCreated", envelopes.Single().EventType);
        Assert.Equal(1, await dbContext.Set<EventStoreRecord>("orders").CountAsync());
    }

    [Fact]
    public async Task AppendAsync_persists_custom_event_id_factory_values()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventIdFactory, CustomEventIdFactory>();
        });
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var envelopes = await eventStore.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var record = await dbContext.Set<EventStoreRecord>("orders").SingleAsync();
        var envelope = Assert.Single(envelopes);
        Assert.Equal(CustomEventIdFactory.EventId, envelope.EventId);
        Assert.Equal(CustomEventIdFactory.EventId, record.EventId);
    }

    [Fact]
    public void AddKrackendEntityFrameworkEventStore_uses_standardized_portable_text_keys_by_default()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        AssertPortableTextKey(
            dbContext.Model.FindEntityType("orders")!,
            nameof(EventStoreRecord.EventId));
        AssertPortableTextKey(
            dbContext.Model.FindEntityType(typeof(EventSnapshotRecord))!,
            nameof(EventSnapshotRecord.SnapshotId));
        AssertPortableTextKey(
            dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord))!,
            nameof(SnapshotCandidateRecord.CandidateId));

        var candidate = dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord))!;
        Assert.Equal(
            nameof(SnapshotCandidateRecord.CandidateId),
            Assert.Single(candidate.FindPrimaryKey()!.Properties).Name);
        Assert.Contains(candidate.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(SnapshotCandidateRecord.StreamName), nameof(SnapshotCandidateRecord.StreamId)]));
    }

    [Fact]
    public void AddKrackendEntityFrameworkEventStore_allows_host_configured_binary_mapping_for_all_keys()
    {
        using var provider = BuildProvider(configureEventStore: options =>
        {
            options.Keys(keys => keys.UseUlidBytes("binary(16)"));
        });
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        AssertBinaryKey(
            dbContext.Model.FindEntityType("orders")!,
            nameof(EventStoreRecord.EventId));
        AssertBinaryKey(
            dbContext.Model.FindEntityType(typeof(EventSnapshotRecord))!,
            nameof(EventSnapshotRecord.SnapshotId));
        AssertBinaryKey(
            dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord))!,
            nameof(SnapshotCandidateRecord.CandidateId));
    }

    [Fact]
    public void AddKrackendEntityFrameworkEventStore_allows_host_to_configure_every_persisted_field()
    {
        using var provider = BuildProvider(configureEventStore: options =>
        {
            options.Keys(keys => keys.UseUlidStrings(30));
            options.Events(events =>
            {
                events.ForAll((entity, store) =>
                {
                    entity.HasAnnotation("Tests:Store", store.Name);
                    AnnotateEveryProperty(entity);
                    entity.Property(x => x.StreamName).HasMaxLength(96);
                });
                events.For("orders", (entity, _) =>
                {
                    entity.HasAnnotation("Tests:SpecificStore", true);
                    entity.Property(x => x.StreamName).HasMaxLength(64);
                    entity.Property(x => x.EventId).HasMaxLength(40);
                });
            });
            options.Snapshots(entity =>
            {
                entity.ToTable("SnapshotsV2", "events");
                AnnotateEveryProperty(entity);
                entity.Property(x => x.Payload).HasMaxLength(8_000);
            });
            options.SnapshotCandidates(entity =>
            {
                entity.ToTable("SnapshotQueue", "events");
                AnnotateEveryProperty(entity);
                entity.Property(x => x.StreamId).HasMaxLength(192);
            });
        });
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var orders = dbContext.Model.FindEntityType("orders")!;
        var payments = dbContext.Model.FindEntityType("payments")!;
        var snapshots = dbContext.Model.FindEntityType(typeof(EventSnapshotRecord))!;
        var candidates = dbContext.Model.FindEntityType(typeof(SnapshotCandidateRecord))!;

        AssertEveryPropertyAnnotated(orders);
        AssertEveryPropertyAnnotated(payments);
        AssertEveryPropertyAnnotated(snapshots);
        AssertEveryPropertyAnnotated(candidates);
        Assert.Equal("orders", orders.FindAnnotation("Tests:Store")!.Value);
        Assert.Equal("payments", payments.FindAnnotation("Tests:Store")!.Value);
        Assert.Equal(true, orders.FindAnnotation("Tests:SpecificStore")!.Value);
        Assert.Null(payments.FindAnnotation("Tests:SpecificStore"));
        Assert.Equal(64, orders.FindProperty(nameof(EventStoreRecord.StreamName))!.GetMaxLength());
        Assert.Equal(96, payments.FindProperty(nameof(EventStoreRecord.StreamName))!.GetMaxLength());
        Assert.Equal(40, orders.FindProperty(nameof(EventStoreRecord.EventId))!.GetMaxLength());
        Assert.Equal(30, payments.FindProperty(nameof(EventStoreRecord.EventId))!.GetMaxLength());
        Assert.Equal("SnapshotsV2", snapshots.GetTableName());
        Assert.Equal("events", snapshots.GetSchema());
        Assert.Equal(8_000, snapshots.FindProperty(nameof(EventSnapshotRecord.Payload))!.GetMaxLength());
        Assert.Equal("SnapshotQueue", candidates.GetTableName());
        Assert.Equal("events", candidates.GetSchema());
        Assert.Equal(192, candidates.FindProperty(nameof(SnapshotCandidateRecord.StreamId))!.GetMaxLength());
    }

    [Fact]
    public async Task AppendRawAsync_persists_untyped_events_in_app_db_context_event_store()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var rawEventStore = scope.ServiceProvider.GetRequiredService<IRawEventStore>();

        await rawEventStore.AppendRawAsync(
            "payments",
            "payment-1",
            ExpectedVersion.NoStream,
            [
                new RawEventData(
                    "Billing.PaymentCaptured",
                    new SemanticVersion(2, 0, 0),
                    "{\"paymentId\":\"payment-1\",\"amount\":250.00}",
                    "{\"sourceSystem\":\"billing\"}")
            ]);

        var envelopes = await rawEventStore.ReadStreamAsync("payments", "payment-1", fromVersion: 1, maxCount: 10);
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var record = await dbContext.Set<EventStoreRecord>("payments").SingleAsync();

        var envelope = Assert.Single(envelopes);
        Assert.Equal("Billing.PaymentCaptured", envelope.EventType);
        Assert.Equal(new SemanticVersion(2, 0, 0), envelope.EventSchemaVersion);
        Assert.Equal("{\"paymentId\":\"payment-1\",\"amount\":250.00}", envelope.Payload);
        Assert.Equal("{\"sourceSystem\":\"billing\"}", record.Metadata);
        Assert.Equal("2.0.0", record.EventSchemaVersion);
    }

    [Fact]
    public async Task AppendAsync_with_any_expected_version_appends_and_updates_current_version()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);

        Assert.Equal(2, await eventStore.GetCurrentVersionAsync("orders", "order-1"));
    }

    [Fact]
    public async Task ReadStreamAsync_reads_stream_range_without_loading_full_stream()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);
        await eventStore.AppendAsync("orders", "order-1", ExpectedVersion.Any, [new OrderCreated("order-1")]);

        var envelopes = await eventStore.ReadStreamAsync("orders", "order-1", fromVersion: 2, maxCount: 1);

        Assert.Single(envelopes);
        Assert.Equal(2, envelopes.Single().StreamVersion);
    }

    [Fact]
    public void EventStoreDbContextFactory_uses_the_scoped_app_db_context()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<Krackend.EventSourcing.EntityFrameworkCore.IEventStoreDbContextFactory<TestDbContext>>();

        Assert.Same(dbContext, factory.CreateDbContext());
    }

    [Fact]
    public async Task Registration_resolves_one_store_for_all_event_store_contracts()
    {
        using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        Assert.Same(eventStore, scope.ServiceProvider.GetRequiredService<IRawEventStore>());
        Assert.Same(eventStore, scope.ServiceProvider.GetRequiredService<IEventLogReader>());
        await Assert.IsType<EntityFrameworkEventStore<TestDbContext>>(eventStore).DisposeAsync();
    }

    [Fact]
    public void EntityFramework_event_store_rejects_null_dependencies()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var stores = services.GetRequiredService<EventStoreOptionsCollection>();
        var envelopeFactory = services.GetRequiredService<IEventEnvelopeFactory>();
        var marker = services.GetRequiredService<ISnapshotCandidateMarker>();
        var dbContextFactory = services.GetRequiredService<IEventStoreDbContextFactory<TestDbContext>>();

        Assert.Throws<ArgumentNullException>(() =>
            new EntityFrameworkEventStore<TestDbContext>(null!, envelopeFactory, marker, dbContextFactory));
        Assert.Throws<ArgumentNullException>(() =>
            new EntityFrameworkEventStore<TestDbContext>(stores, null!, marker, dbContextFactory));
        Assert.Throws<ArgumentNullException>(() =>
            new EntityFrameworkEventStore<TestDbContext>(stores, envelopeFactory, null!, dbContextFactory));
        Assert.Throws<ArgumentNullException>(() =>
            new EntityFrameworkEventStore<TestDbContext>(stores, envelopeFactory, marker, null!));
    }

    [Fact]
    public async Task AppendAsync_rejects_a_stale_expected_version()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]);

        await Assert.ThrowsAsync<EventStoreConcurrencyException>(() =>
            eventStore.AppendAsync("orders", "order-1", 0, [new OrderCreated("order-1")]));
    }

    [Fact]
    public void Krackend_model_extension_exposes_stable_service_provider_metadata()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var dbContextOptions = dbContext.GetService<IDbContextOptions>();
        var extensionInfo = Assert.Single(
            dbContextOptions.Extensions
                .Select(extension => extension.Info)
                .Where(info => info.LogFragment.Contains("KrackendEventStore", StringComparison.Ordinal)));
        var debugInfo = new Dictionary<string, string>();

        extensionInfo.PopulateDebugInfo(debugInfo);

        Assert.Equal("using KrackendEventStore ", extensionInfo.LogFragment);
        Assert.Equal("orders,payments", debugInfo["KrackendEventStore"]);
        Assert.False(extensionInfo.IsDatabaseProvider);
        Assert.NotEqual(0, extensionInfo.GetServiceProviderHashCode());
        Assert.False(extensionInfo.ShouldUseSameServiceProvider(
            dbContextOptions.Extensions.First(extension => extension.Info.IsDatabaseProvider).Info));
    }

    [Fact]
    public async Task EntityFramework_snapshot_stores_use_app_db_context_model()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var snapshotStore = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
        var candidateStore = scope.ServiceProvider.GetRequiredService<ISnapshotCandidateStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await snapshotStore.SaveAsync(new Snapshot(
            "orders",
            "order-1",
            3,
            "OrderState",
            "1.0.0",
            "{}",
            DateTimeOffset.UtcNow));
        await candidateStore.MarkAsync("orders", "order-1", 4);
        await candidateStore.MarkAsync("orders", "order-1", 3);
        await candidateStore.MarkAsync("orders", "order-1", 5);

        Assert.Equal(1, await dbContext.Set<EventSnapshotRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<SnapshotCandidateRecord>().CountAsync());
        var snapshot = await dbContext.Set<EventSnapshotRecord>().SingleAsync();
        var candidate = await dbContext.Set<SnapshotCandidateRecord>().SingleAsync();
        Assert.True(Ulid.TryParse(snapshot.SnapshotId, out _));
        Assert.True(Ulid.TryParse(candidate.CandidateId, out _));
        Assert.Equal("OrderState", snapshot.StateType);
        Assert.Equal("1.0.0", snapshot.StateSchemaVersion);
        Assert.Equal(5, candidate.StreamVersion);

        var pending = Assert.Single(await candidateStore.GetPendingAsync(10));
        await candidateStore.CompleteAsync(pending);
        await candidateStore.CompleteAsync(pending);
        Assert.Empty(await candidateStore.GetPendingAsync(10));
    }

    private static void AnnotateEveryProperty<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        foreach (var property in entity.Metadata.GetProperties())
            entity.Property(property.Name).HasAnnotation("Tests:Configured", true);
    }

    private static void AssertEveryPropertyAnnotated(IEntityType entity)
    {
        Assert.NotEmpty(entity.GetProperties());
        Assert.All(entity.GetProperties(), property =>
            Assert.Equal(true, property.FindAnnotation("Tests:Configured")?.Value));
    }

    private static void AssertPortableTextKey(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName)!;
        Assert.Null(property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.Null(property.GetValueConverter());
        Assert.Equal(26, property.GetMaxLength());
        Assert.Equal(property, Assert.Single(entity.FindPrimaryKey()!.Properties));
    }

    private static void AssertBinaryKey(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName)!;
        Assert.Equal("binary(16)", property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
        Assert.Equal(16, property.GetMaxLength());

        var converter = Assert.IsType<UlidStringToBytesConverter>(property.GetValueConverter());
        Assert.Equal(typeof(byte[]), converter.ProviderClrType);

        var providerValue = Assert.IsType<byte[]>(converter.ConvertToProvider(BinaryEventId));
        Assert.Equal(16, providerValue.Length);
        Assert.Equal(BinaryEventId, converter.ConvertFromProvider(providerValue));
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection>? configureServices = null,
        Action<EntityFrameworkEventStoreOptions>? configureEventStore = null)
    {
        var services = new ServiceCollection();

        configureServices?.Invoke(services);

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddKrackendEventSourcing(options =>
        {
            options.ScanAssemblyContaining<EntityFrameworkEventStoreTests>();
            options.Stores.Add("orders", store => store.TableName = "OrderEvents");
            options.Stores.Add("payments", store => store.TableName = "PaymentEvents");
        });

        services.AddKrackendEntityFrameworkEventStore<TestDbContext>(configureEventStore);

        return services.BuildServiceProvider();
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }
    }

    [EventSchema("OrderCreated")]
    private sealed record OrderCreated(string OrderId);

    private sealed class CustomEventIdFactory : IEventIdFactory
    {
        public const string EventId = "custom:orders:order-1:1";

        public string Create(EventIdFactoryContext context)
            => EventId;
    }

    private const string BinaryEventId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
}
