namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;

internal sealed class InMemoryRuntimeExtensionPackageRepository : IRuntimeExtensionPackageRepository
{
    private readonly InMemoryRuntimeStore _store;

    public InMemoryRuntimeExtensionPackageRepository(InMemoryRuntimeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task UpsertAsync(RuntimeExtensionPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        _store.ExtensionPackages[package.Id] = package;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<RuntimeExtensionPackage>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeExtensionPackage>>(
            _store.ExtensionPackages.Values.ToArray());

    public Task<RuntimeExtensionPackage> TryGetActiveAsync(
        string extensionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default)
    {
        var package = _store.ExtensionPackages.Values.FirstOrDefault(candidate =>
            candidate.Status == RuntimeExtensionPackageStatus.Activated &&
            string.Equals(candidate.ExtensionKey, extensionKey, StringComparison.OrdinalIgnoreCase) &&
            candidate.Version.Equals(version));

        return Task.FromResult(package);
    }
}

