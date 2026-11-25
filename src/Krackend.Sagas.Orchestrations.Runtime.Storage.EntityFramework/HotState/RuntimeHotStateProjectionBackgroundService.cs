#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class RuntimeHotStateProjectionBackgroundService(
    IRuntimeHotStateStore hotState,
    IServiceScopeFactory scopeFactory,
    ILogger<RuntimeHotStateProjectionBackgroundService> logger,
    IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions,
    RuntimeHotStateProjectionActivity activity) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!hotState.IsEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            RuntimeHotProjectionLease? lease = null;
            try
            {
                // A quiet window lets a callback burst collapse to its final entity
                // versions without a bulk SQL write preempting realtime work. The max
                // delay still forces progress under uninterrupted traffic.
                var interval = storageOptions.Value.HotStateProjectionInterval < TimeSpan.FromMilliseconds(10)
                    ? TimeSpan.FromMilliseconds(10)
                    : storageOptions.Value.HotStateProjectionInterval;
                var maximumDelay = storageOptions.Value.HotStateProjectionMaxDelay < interval
                    ? interval
                    : storageOptions.Value.HotStateProjectionMaxDelay;
                var batchSize = Math.Clamp(storageOptions.Value.HotStateProjectionBatchSize, 1, 100_000);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250, interval.TotalMilliseconds)), stoppingToken);
                if (activity.ShouldDefer(
                    interval,
                    maximumDelay,
                    storageOptions.Value.AllowHotStateProjectionDuringForeground))
                    continue;
                lease = await hotState.TryAcquireProjectionLeaseAsync(batchSize, TimeSpan.FromSeconds(30), stoppingToken);
                if (lease is null)
                {
                    activity.MarkDrained();
                    await hotState.CleanupProjectedTerminalStateAsync(100, stoppingToken);
                    continue;
                }
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRuntimeHotStateProjectionSink>()
                    .ProjectAsync(lease.Batches, stoppingToken);
                await hotState.AcknowledgeProjectionAsync(lease, stoppingToken);
                lease = null;
                await hotState.CleanupProjectedTerminalStateAsync(100, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Runtime hot-state durable projection failed.");
                if (lease is not null)
                {
                    try { await hotState.ReleaseProjectionLeaseAsync(lease, CancellationToken.None); }
                    catch (Exception releaseException)
                    {
                        logger.LogWarning(releaseException, "Runtime hot-state projection lease could not be released.");
                    }
                }
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
