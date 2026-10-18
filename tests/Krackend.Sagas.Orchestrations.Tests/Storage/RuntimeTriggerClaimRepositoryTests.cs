namespace Krackend.Sagas.Orchestrations.Tests.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class RuntimeTriggerClaimRepositoryTests
{
    [Fact]
    public async Task ClaimAsync_UsesSharedPersistenceToRejectAnOwnerFromAnotherRuntimeHost()
    {
        await WithHostsAsync(async (firstHost, secondHost) =>
        {
            var firstArtifactId = Id.New();
            var secondArtifactId = Id.New();
            var first = await ClaimAsync(firstHost, ["messaging|events.sales.sale.created|1.0.0"], firstArtifactId, []);
            var second = await ClaimAsync(secondHost, ["MESSAGING|EVENTS.SALES.SALE.CREATED|1.0.0"], secondArtifactId, []);

            Assert.True(first.Succeeded);
            Assert.False(second.Succeeded);
            Assert.Equal(firstArtifactId, second.ConflictingArtifactId);
        });
    }

    [Fact]
    public async Task ClaimAsync_AllowsAnExplicitRolloutOwnerToTransferTheDurableClaim()
    {
        await WithHostsAsync(async (firstHost, secondHost) =>
        {
            var retiringArtifactId = Id.New();
            var replacementArtifactId = Id.New();
            await ClaimAsync(firstHost, ["messaging|events.sales.sale.created|1.0.0"], retiringArtifactId, []);
            var replacement = await ClaimAsync(
                secondHost,
                ["messaging|events.sales.sale.created|1.0.0"],
                replacementArtifactId,
                [retiringArtifactId]);
            var retiredOwner = await ClaimAsync(
                firstHost,
                ["messaging|events.sales.sale.created|1.0.0"],
                retiringArtifactId,
                []);

            Assert.True(replacement.Succeeded);
            Assert.False(retiredOwner.Succeeded);
            Assert.Equal(replacementArtifactId, retiredOwner.ConflictingArtifactId);
        });
    }

    [Fact]
    public async Task ReleaseAsync_RemovesClaimsSoANewActiveVersionCanOwnTheTrigger()
    {
        await WithHostsAsync(async (firstHost, secondHost) =>
        {
            var retiredArtifactId = Id.New();
            var replacementArtifactId = Id.New();
            await ClaimAsync(firstHost, ["messaging|events.sales.sale.created|1.0.0"], retiredArtifactId, []);
            await ReleaseAsync(firstHost, [retiredArtifactId]);
            var replacement = await ClaimAsync(
                secondHost,
                ["messaging|events.sales.sale.created|1.0.0"],
                replacementArtifactId,
                []);

            Assert.True(replacement.Succeeded);
        });
    }

    private static ServiceProvider CreateProvider(string databasePath)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsRuntime();
        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        return services.BuildServiceProvider();
    }

    private static async Task EnsureDatabaseAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>().Database;
        await database.EnsureCreatedAsync();
    }

    private static async Task<RuntimeTriggerClaimResult> ClaimAsync(
        ServiceProvider provider,
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>()
            .ClaimAsync(identities, artifactId, replaceableArtifactIds);
    }

    private static async Task ReleaseAsync(ServiceProvider provider, IReadOnlyCollection<Id> artifactIds)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRuntimeTriggerClaimRepository>().ReleaseAsync(artifactIds);
    }

    private static async Task WithHostsAsync(Func<ServiceProvider, ServiceProvider, Task> action)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"krackend-trigger-claims-{Guid.NewGuid():N}.db");
        try
        {
            await using (var firstHost = CreateProvider(databasePath))
            await using (var secondHost = CreateProvider(databasePath))
            {
                await EnsureDatabaseAsync(firstHost);
                await action(firstHost, secondHost);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
