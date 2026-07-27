using Krackend.EventSourcing.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.EventSourcing.EntityFrameworkCore.Internal;

internal sealed class KrackendEventStoreOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public KrackendEventStoreOptionsExtension(EventStoreOptionsCollection stores)
    {
        Stores = stores ?? throw new ArgumentNullException(nameof(stores));
    }

    public EventStoreOptionsCollection Stores { get; }

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IModelCustomizer, KrackendEventStoreModelCustomizer>());
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        private readonly KrackendEventStoreOptionsExtension _extension;

        public ExtensionInfo(KrackendEventStoreOptionsExtension extension)
            : base(extension)
        {
            _extension = extension;
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "using KrackendEventStore ";

        public override int GetServiceProviderHashCode()
            => HashCode.Combine(
                typeof(KrackendEventStoreOptionsExtension),
                string.Join('|', _extension.Stores.Values.Keys.OrderBy(x => x)));

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["KrackendEventStore"] = string.Join(",", _extension.Stores.Values.Keys.OrderBy(x => x));

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
