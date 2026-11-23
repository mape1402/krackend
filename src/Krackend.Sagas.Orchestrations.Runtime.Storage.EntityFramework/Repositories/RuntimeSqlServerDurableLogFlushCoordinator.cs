#nullable enable

using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Coalesces independent SQL Server delayed-durability commits into explicit durable
/// log flushes. This is a durability barrier, not a worker queue: transaction work and
/// commits remain concurrent, and callers only meet after their commit has completed.
/// </summary>
internal sealed class RuntimeSqlServerDurableLogFlushCoordinator
{
    private readonly ConcurrentDictionary<string, DatabaseFlushState> _databases =
        new(StringComparer.OrdinalIgnoreCase);

    public Task FlushAsync(
        RuntimeDbContext dbContext,
        TimeSpan collectionWindow,
        int maxConcurrentPhysicalFlushes)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (collectionWindow < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(collectionWindow));
        if (maxConcurrentPhysicalFlushes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentPhysicalFlushes));

        var connection = dbContext.Database.GetDbConnection();
        var connectionString = dbContext.Database.GetConnectionString()
            ?? connection.ConnectionString;
        var providerFactory = DbProviderFactories.GetFactory(connection)
            ?? throw new InvalidOperationException("The database connection has no provider factory.");
        var databaseKey = string.Concat(connection.DataSource, "\n", connection.Database);
        var state = _databases.GetOrAdd(
            databaseKey,
            static (_, concurrency) => new DatabaseFlushState(concurrency),
            maxConcurrentPhysicalFlushes);
        FlushBatch batch;

        lock (state.Gate)
        {
            var startBatch = state.Current is null;
            batch = state.Current ??= new FlushBatch(
                providerFactory,
                connectionString,
                collectionWindow);
            batch.MemberCount++;
            if (startBatch)
                _ = RunFlushAsync(state, batch);
        }

        return AwaitDurableBarrierAsync(batch.Completion.Task);
    }

    private static async Task AwaitDurableBarrierAsync(Task completion)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await completion;
        }
        finally
        {
            RuntimeConcurrencyMetrics.StorageDurableLogBarrierMilliseconds.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static async Task RunFlushAsync(DatabaseFlushState state, FlushBatch batch)
    {
        if (batch.CollectionWindow > TimeSpan.Zero)
            await Task.Delay(batch.CollectionWindow, CancellationToken.None);

        // Keep this batch open while all physical flush slots are occupied. Every
        // commit joining it has already completed, so one subsequent sp_flush_log is
        // the durability barrier for the entire group. Saga work itself is never
        // queued here; only redundant physical fsync requests are coalesced.
        await state.PhysicalFlushSlots.WaitAsync(CancellationToken.None);

        // Close membership before issuing sp_flush_log. A transaction that commits
        // after this point joins a new independent batch. SQL Server may coalesce
        // overlapping flush requests internally without blocking transaction work.
        lock (state.Gate)
        {
            if (ReferenceEquals(state.Current, batch))
                state.Current = null;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            // Use an independent pooled connection so the producing DbContext and its
            // committed transaction can be disposed while the durable barrier runs.
            // This is what allows the next already-persisted dispatch to overlap disk
            // latency without allowing the Rabbit delivery to be acknowledged early.
            await using var connection = batch.ProviderFactory.CreateConnection()
                ?? throw new InvalidOperationException("The database provider did not create a connection.");
            connection.ConnectionString = batch.ConnectionString;
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sys.sp_flush_log;";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
            batch.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            batch.Completion.TrySetException(exception);
        }
        finally
        {
            RuntimeConcurrencyMetrics.StorageDurableLogFlushMilliseconds.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            RuntimeConcurrencyMetrics.StorageDurableLogFlushBatchSize.Record(
                batch.MemberCount);
            state.PhysicalFlushSlots.Release();
        }
    }

    private sealed class DatabaseFlushState(int maxConcurrentPhysicalFlushes)
    {
        public object Gate { get; } = new();
        public FlushBatch? Current { get; set; }
        public SemaphoreSlim PhysicalFlushSlots { get; } = new(
            maxConcurrentPhysicalFlushes,
            maxConcurrentPhysicalFlushes);
    }

    private sealed class FlushBatch(
        DbProviderFactory providerFactory,
        string connectionString,
        TimeSpan collectionWindow)
    {
        public DbProviderFactory ProviderFactory { get; } = providerFactory;
        public string ConnectionString { get; } = connectionString;
        public TimeSpan CollectionWindow { get; } = collectionWindow;
        public long MemberCount { get; set; }
        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
