using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class OrchestrationLifetimeBackgroundService(
    IServiceScopeFactory scopes,
    IOptionsMonitor<OrchestrationTimeoutOptions> options,
    ILogger<OrchestrationLifetimeBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backfillComplete = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.CurrentValue.Enabled)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var admission = scope.ServiceProvider.GetService<IRuntimeAdmissionController>();
                    if (admission is null || admission.CanAccept(RuntimeAdmissionOperation.TimeoutProcessing))
                    {
                        if (!backfillComplete)
                            backfillComplete = await scope.ServiceProvider.GetRequiredService<OrchestrationLifetimeBackfill>()
                                .ProcessAsync(stoppingToken);
                        var processed = await scope.ServiceProvider.GetRequiredService<OrchestrationLifetimeProcessor>()
                            .ProcessAsync(DateTime.UtcNow, stoppingToken);
                        logger.LogInformation("Orchestration lifetime scan moved {InstanceCount} instance(s) to idle.", processed);
                        var retired = await scope.ServiceProvider.GetRequiredService<ArchivedRecoveryRetirementProcessor>()
                            .ProcessAsync(stoppingToken);
                        logger.LogInformation("Archived recovery scan retired {ArtifactCount} backchannel(s).", retired);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Could not scan orchestration instance lifetimes."); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.InstanceLifetimeScanIntervalSeconds)), stoppingToken);
        }
    }
}
