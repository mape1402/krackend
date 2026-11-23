using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

internal sealed class RuntimeHistoryRetentionBackgroundService(
    IServiceScopeFactory scopes,
    IOptionsMonitor<RuntimeHistoryRetentionOptions> options,
    TimeProvider clock,
    ILogger<RuntimeHistoryRetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Enabled) return;
        await Task.Delay(
            TimeSpan.FromSeconds(Math.Clamp(options.CurrentValue.InitialDelaySeconds, 1, 3_600)),
            clock,
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var current = options.CurrentValue;
            if (current.Enabled)
            {
                try
                {
                    await SweepAsync(current, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Runtime history retention sweep failed.");
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Clamp(current.SweepIntervalSeconds, 10, 86_400)),
                clock,
                stoppingToken);
        }
    }

    private async Task SweepAsync(RuntimeHistoryRetentionOptions current, CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(current.BatchSize, 1, 5_000);
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-Math.Clamp(current.CompletedRetentionHours, 1, 87_600));
        var removedInstances = 0;
        var removedRows = 0;
        for (var batch = 0; batch < Math.Clamp(current.MaxBatchesPerSweep, 1, 100); batch++)
        {
            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IRuntimeHistoryRetentionProcessor>()
                .ProcessAsync(cutoff, batchSize, cancellationToken);
            removedInstances += result.RemovedInstances;
            removedRows += result.RemovedRows;
            if (result.CandidateInstances < batchSize) break;
            await Task.Yield();
        }

        if (removedInstances > 0)
        {
            logger.LogInformation(
                "Runtime history retention removed {InstanceCount} successful execution graph(s) and {RowCount} total row(s).",
                removedInstances,
                removedRows);
        }
    }
}
