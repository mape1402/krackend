#nullable enable

using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeProviderDurabilityInterceptor : SaveChangesInterceptor
{
    private readonly RuntimeEntityFrameworkStorageOptions _options;

    public RuntimeProviderDurabilityInterceptor(
        IOptions<RuntimeEntityFrameworkStorageOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        AwaitIfRequired(eventData.Context).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await AwaitIfRequired(eventData.Context);
        return result;
    }

    private Task AwaitIfRequired(DbContext? context)
    {
        if (context is not RuntimeDbContext runtimeDbContext ||
            runtimeDbContext.IsProviderDurabilityDeferred ||
            runtimeDbContext.Database.CurrentTransaction is not null ||
            _options.ProviderOptimizations?.AwaitDurabilityAsync is not { } barrier)
            return Task.CompletedTask;

        return barrier(runtimeDbContext);
    }
}
