namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed class RuntimeExtensionPackageRepository : RuntimeRepositoryBase, IRuntimeExtensionPackageRepository
{
    public RuntimeExtensionPackageRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task UpsertAsync(RuntimeExtensionPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var current = await DbContext.RuntimeExtensionPackages.FirstOrDefaultAsync(
            x => x.Id == package.Id,
            cancellationToken);
        if (current is null)
        {
            DbContext.RuntimeExtensionPackages.Add(package);
        }
        else
        {
            current.BundleId = package.BundleId;
            current.ExtensionKey = package.ExtensionKey;
            current.Version = package.Version;
            current.Sha256 = package.Sha256;
            current.SizeBytes = package.SizeBytes;
            current.Manifest = package.Manifest;
            current.Status = package.Status;
            current.CreatedOnUtc = package.CreatedOnUtc;
            current.UpdatedOnUtc = package.UpdatedOnUtc;
            current.ActivatedOnUtc = package.ActivatedOnUtc;
            current.StatusReason = package.StatusReason;
        }

        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeExtensionPackage>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbContext.RuntimeExtensionPackages.AsNoTracking()
            .OrderBy(x => x.ExtensionKey)
            .ThenBy(x => x.BundleId)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeExtensionPackage> TryGetActiveAsync(
        string extensionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default)
    {
        var candidates = await DbContext.RuntimeExtensionPackages.AsNoTracking()
            .Where(
                x => x.Status == RuntimeExtensionPackageStatus.Activated &&
                    x.Version.Equals(version))
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.ExtensionKey, extensionKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<RuntimeExtensionPackage> TryGetActiveBundleAsync(
        string bundleId,
        string extensionKey,
        SemanticVersion version,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var candidates = await DbContext.RuntimeExtensionPackages.AsNoTracking()
            .Where(
                x => x.Status == RuntimeExtensionPackageStatus.Activated &&
                    x.Version.Equals(version))
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.BundleId, bundleId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.ExtensionKey, extensionKey, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(sha256) ||
                string.Equals(candidate.Sha256, sha256, StringComparison.OrdinalIgnoreCase)));
    }
}
