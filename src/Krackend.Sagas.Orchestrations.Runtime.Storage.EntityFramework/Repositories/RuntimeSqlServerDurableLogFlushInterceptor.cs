#nullable enable

using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Makes every implicit SQL Server SaveChanges transaction durable before returning,
/// including repository writes that intentionally bypass the runtime unit of work.
/// Explicit transactions are flushed by their commit boundary instead.
/// </summary>
internal sealed class RuntimeSqlServerDurableLogFlushInterceptor : SaveChangesInterceptor
{
    private readonly RuntimeEntityFrameworkStorageOptions _options;
    private readonly RuntimeSqlServerDurableLogFlushCoordinator _coordinator;

    public RuntimeSqlServerDurableLogFlushInterceptor(
        IOptions<RuntimeEntityFrameworkStorageOptions> options,
        RuntimeSqlServerDurableLogFlushCoordinator coordinator)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushIfRequired(eventData.Context).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushIfRequired(eventData.Context);
        return result;
    }

    private Task FlushIfRequired(DbContext? context)
    {
        if (!_options.CoordinateSqlServerDurableLogFlushes ||
            context is not RuntimeDbContext runtimeDbContext ||
            runtimeDbContext.IsDurableLogFlushDeferred ||
            runtimeDbContext.Database.CurrentTransaction is not null ||
            !string.Equals(
                runtimeDbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
            return Task.CompletedTask;

        // SaveChanges has committed its implicit transaction before this callback.
        // Ignore caller cancellation from here onward: durability cannot be optional
        // after the database has accepted the commit.
        return _coordinator.FlushAsync(
            runtimeDbContext,
            _options.SqlServerDurableLogFlushWindow,
            _options.SqlServerDurableLogFlushConcurrency);
    }
}
