using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Krackend.EventSourcing.Tests;

public sealed class EntityFrameworkEventStoreOptionsTests
{
    [Fact]
    public void Mapping_options_reject_invalid_configuration()
    {
        var options = new EntityFrameworkEventStoreOptions();
        var keys = new EntityFrameworkEventStoreKeyOptions();
        var events = new EntityFrameworkEventStoreEntityOptions();

        Assert.Throws<ArgumentNullException>(() => options.Keys(null!));
        Assert.Throws<ArgumentNullException>(() => options.Events(null!));
        Assert.Throws<ArgumentNullException>(() => options.Snapshots(null!));
        Assert.Throws<ArgumentNullException>(() => options.SnapshotCandidates(null!));
        Assert.Throws<ArgumentNullException>(() => keys.Configure(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.UseUlidStrings(0));
        Assert.Throws<ArgumentNullException>(() => events.ForAll(null!));
        Assert.Throws<ArgumentNullException>(() => events.For(null!, (_, _) => { }));
        Assert.Throws<ArgumentException>(() => events.For(" ", (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => events.For("orders", null!));
    }

    [Fact]
    public void Ulid_converter_rejects_non_ulid_strings_and_invalid_byte_lengths()
    {
        var converter = new UlidStringToBytesConverter();

        var invalidString = Assert.Throws<InvalidOperationException>(
            () => converter.ConvertToProvider("custom:orders:order-1:1"));
        var invalidBytes = Assert.Throws<InvalidOperationException>(
            () => converter.ConvertFromProvider(new byte[15]));

        Assert.Contains("valid ULID", invalidString.Message);
        Assert.Contains("exactly 16 bytes", invalidBytes.Message);
    }

    [Fact]
    public void HasUlidBytesConversion_accepts_portable_provider_mapping()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var property = modelBuilder.Entity<UlidRecord>().Property(x => x.Id);

        var returned = property.HasUlidBytesConversion(" ");

        Assert.Same(property, returned);
        Assert.IsType<UlidStringToBytesConverter>(property.Metadata.GetValueConverter());
        Assert.Null(property.Metadata.FindAnnotation(RelationalAnnotationNames.ColumnType));
        Assert.Throws<ArgumentNullException>(
            () => UlidPropertyBuilderExtensions.HasUlidBytesConversion(null!));
    }

    [Fact]
    public void AddKrackendEventStore_uses_default_mapping_options_when_none_are_supplied()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var stores = new EventStoreOptionsCollection().Add("orders");

        var returned = modelBuilder.AddKrackendEventStore(stores);

        Assert.Same(modelBuilder, returned);
        Assert.Equal(
            26,
            modelBuilder.Model.FindEntityType("orders")!
                .FindProperty(nameof(EventStoreRecord.EventId))!
                .GetMaxLength());
        Assert.Throws<ArgumentNullException>(() =>
            EventStoreEntityModelBuilderExtensions.AddKrackendEventStore(null!, stores));
        Assert.Throws<ArgumentNullException>(() => modelBuilder.AddKrackendEventStore(null!));
    }

    [Fact]
    public void Registration_supports_a_db_context_options_instance_descriptor()
    {
        var services = new ServiceCollection();
        var original = new DbContextOptionsBuilder<InstanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        services.AddSingleton(original);
        services.AddKrackendEventSourcing(options => options.Stores.Add("orders"));
        services.AddKrackendEntityFrameworkEventStore<InstanceDbContext>();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<DbContextOptions<InstanceDbContext>>();

        Assert.NotSame(original, resolved);
        Assert.Contains(resolved.Extensions, extension =>
            extension.GetType().Name == "InMemoryOptionsExtension");
    }

    [Fact]
    public void Registration_supports_a_db_context_options_implementation_type_descriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DbContextOptions<InstanceDbContext>>();
        services.AddKrackendEventSourcing(options => options.Stores.Add("orders"));
        services.AddKrackendEntityFrameworkEventStore<InstanceDbContext>();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<DbContextOptions<InstanceDbContext>>();

        Assert.NotEmpty(resolved.Extensions);
    }

    [Fact]
    public void Registration_rejects_an_invalid_db_context_options_descriptor()
    {
        var services = new ServiceCollection();
        ((IServiceCollection)services).Add(ServiceDescriptor.Singleton(
            typeof(DbContextOptions<InstanceDbContext>),
            new object()));
        services.AddKrackendEventSourcing(options => options.Stores.Add("orders"));
        services.AddKrackendEntityFrameworkEventStore<InstanceDbContext>();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<DbContextOptions<InstanceDbContext>>());

        Assert.Contains(nameof(InstanceDbContext), exception.Message);
    }

    [Fact]
    public void EventStoreDbContextFactory_rejects_null_dependencies()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<InstanceDbContext>().Options;

        Assert.Throws<ArgumentNullException>(() =>
            new EventStoreDbContextFactory<InstanceDbContext>(null!, options));
        Assert.Throws<ArgumentNullException>(() =>
            new EventStoreDbContextFactory<InstanceDbContext>(provider, null!));
    }

    [Fact]
    public void Internal_options_extension_rejects_null_dependencies()
    {
        var extensionType = typeof(EntityFrameworkEventStoreOptions).Assembly.GetType(
            "Krackend.EventSourcing.EntityFrameworkCore.Internal.KrackendEventStoreOptionsExtension",
            throwOnError: true)!;

        AssertConstructorArgumentNull(
            extensionType,
            [null, new EntityFrameworkEventStoreOptions()],
            "stores");
        AssertConstructorArgumentNull(
            extensionType,
            [new EventStoreOptionsCollection(), null],
            "options");
    }

    [Fact]
    public void Registration_requires_the_host_db_context_to_be_registered_first()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddKrackendEntityFrameworkEventStore<MissingDbContext>());

        Assert.Contains(nameof(MissingDbContext), exception.Message);
        Assert.Throws<ArgumentNullException>(
            () => EntityFrameworkEventStoreServiceCollectionExtensions
                .AddKrackendEntityFrameworkEventStore<MissingDbContext>(null!));
    }

    private sealed class UlidRecord
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class MissingDbContext : DbContext;

    private sealed class InstanceDbContext : DbContext
    {
        public InstanceDbContext(DbContextOptions<InstanceDbContext> options)
            : base(options)
        {
        }
    }

    private static void AssertConstructorArgumentNull(
        Type extensionType,
        object?[] arguments,
        string parameterName)
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            Activator.CreateInstance(
                extensionType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: arguments,
                culture: null));

        var argumentException = Assert.IsType<ArgumentNullException>(exception.InnerException);
        Assert.Equal(parameterName, argumentException.ParamName);
    }
}
