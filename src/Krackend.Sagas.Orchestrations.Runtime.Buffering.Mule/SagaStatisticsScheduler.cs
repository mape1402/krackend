using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

internal sealed class SagaStatisticsScheduler(IServiceScopeFactory scopes, IOptions<SagaStatisticsMuleOptions> options,
    ILogger<SagaStatisticsScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var interval = Math.Clamp(options.Value.IntervalSeconds, 1, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (scope.ServiceProvider.GetService<ISagaStatisticsBatchProcessor>() is null) return;
                var client = scope.ServiceProvider.GetRequiredService<IMuleClient>();
                var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / interval;
                await client.EnqueueAsync(MuleActionKeys.StatisticsBatchActionKey, new SagaStatisticsBatchRequest { Window = window },
                    configure =>
                    {
                        configure.Lane = MuleActionKeys.StatisticsLane;
                        configure.DeduplicationKey = $"saga-statistics:{interval}:{window}";
                    }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not schedule the next statistics batch."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
