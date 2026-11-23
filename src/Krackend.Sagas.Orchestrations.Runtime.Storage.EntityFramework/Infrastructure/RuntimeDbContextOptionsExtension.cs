using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class RuntimeDbContextOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo _info;

    public RuntimeDbContextOptionsExtension(
        RuntimeEntityFrameworkStorageOptions storageOptions,
        RuntimeMutationLeaseScope mutationLease)
    {
        StorageOptions = storageOptions ?? throw new ArgumentNullException(nameof(storageOptions));
        MutationLease = mutationLease;
    }

    public RuntimeEntityFrameworkStorageOptions StorageOptions { get; }
    public RuntimeMutationLeaseScope MutationLease { get; }
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);
    public void ApplyServices(IServiceCollection services) { }
    public void Validate(IDbContextOptions options) { }

    private sealed class ExtensionInfo(RuntimeDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "using KrackendRuntimeStorage ";
        public override int GetServiceProviderHashCode()
            => HashCode.Combine(
                typeof(RuntimeDbContextOptionsExtension),
                extension.StorageOptions.ConfigureModel,
                extension.StorageOptions.CompressSnapshotPayload);
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["KrackendRuntimeStorage"] = GetServiceProviderHashCode().ToString();
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo candidate &&
               candidate.GetServiceProviderHashCode() == GetServiceProviderHashCode();
    }
}

internal static class RuntimeDbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseKrackendRuntimeStorage(
        this DbContextOptionsBuilder builder,
        RuntimeEntityFrameworkStorageOptions storageOptions,
        RuntimeMutationLeaseScope mutationLease)
    {
        var extension = new RuntimeDbContextOptionsExtension(storageOptions, mutationLease);
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder;
    }
}
