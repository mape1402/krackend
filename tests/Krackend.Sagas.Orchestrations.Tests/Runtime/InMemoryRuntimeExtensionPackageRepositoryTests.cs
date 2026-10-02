namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;

public sealed class InMemoryRuntimeExtensionPackageRepositoryTests
{
    [Fact]
    public async Task TryGetActiveAsyncReturnsOnlyActivatedMatchingPackage()
    {
        var repository = new InMemoryRuntimeExtensionPackageRepository(new InMemoryRuntimeStore());
        var version = new SemanticVersion(1, 2, 3);
        await repository.UpsertAsync(Package("contoso.billing", version, RuntimeExtensionPackageStatus.Disabled));
        var activated = Package("contoso.billing", version, RuntimeExtensionPackageStatus.Activated);
        await repository.UpsertAsync(activated);

        var result = await repository.TryGetActiveAsync("CONTOSO.BILLING", version);

        Assert.NotNull(result);
        Assert.Equal(activated.Id, result.Id);
    }

    [Fact]
    public async Task GetAllAsyncReturnsStoredPackages()
    {
        var repository = new InMemoryRuntimeExtensionPackageRepository(new InMemoryRuntimeStore());
        await repository.UpsertAsync(Package("contoso.billing", new SemanticVersion(1, 0, 0), RuntimeExtensionPackageStatus.Activated));
        await repository.UpsertAsync(Package("contoso.crm", new SemanticVersion(2, 0, 0), RuntimeExtensionPackageStatus.Validated));

        var packages = await repository.GetAllAsync();

        Assert.Equal(2, packages.Count);
    }

    private static RuntimeExtensionPackage Package(
        string extensionKey,
        SemanticVersion version,
        RuntimeExtensionPackageStatus status)
        => new()
        {
            Id = Id.New(),
            BundleId = $"bundle-{extensionKey}-{version}",
            ExtensionKey = extensionKey,
            Version = version,
            Sha256 = "sha",
            SizeBytes = 128,
            Status = status,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            ActivatedOnUtc = status == RuntimeExtensionPackageStatus.Activated ? DateTime.UtcNow : null,
            Manifest = new KrackendExtensionManifest(
                new ExtensionKey(extensionKey),
                version,
                extensionKey,
                "tests",
                ExtensionLoadMode.ExternalAssembly,
                ExtensionTrustLevel.PublisherTrusted)
        };
}

