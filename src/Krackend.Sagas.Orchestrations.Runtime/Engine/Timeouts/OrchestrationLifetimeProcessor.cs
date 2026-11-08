using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class OrchestrationLifetimeProcessor(
    IOrchestrationInstanceRepository instances,
    IOrchestrationInstanceCoordinator coordinator,
    IDecisionHandler<EnterIdleInstanceDecision> idle,
    IOptions<OrchestrationTimeoutOptions> options,
    ILogger<OrchestrationLifetimeProcessor> logger,
    IServiceScopeFactory scopes = null)
{
    public async Task<int> ProcessAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (instances is not IOrchestrationExpirationRepository expiration)
        {
            logger.LogWarning("The instance repository does not support projected expiration scans.");
            return 0;
        }
        var pageSize = Math.Clamp(options.Value.InstanceLifetimeBatchSize, 1, 1000);
        var processed = 0;
        var skip = 0;
        while (true)
        {
            var page = await expiration.GetExpiredInstances(utcNow, skip, pageSize, cancellationToken);
            if (page.Count == 0) break;
            using var batchScope = scopes?.CreateScope();
            var batchInstances = batchScope?.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>() ?? instances;
            var batchCoordinator = batchScope?.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>() ?? coordinator;
            var batchIdle = batchScope?.ServiceProvider.GetRequiredService<IDecisionHandler<EnterIdleInstanceDecision>>() ?? idle;
            var removed = 0;
            foreach (var id in page)
            {
                var wasRemoved = false;
                try
                {
                    await batchCoordinator.TryExecuteAsync(id, async token =>
                    {
                        var instance = await batchInstances.GetById(id, token);
                        if (instance.Status != OrchestrationInstanceStatus.Running || instance.ExpiresOnUtc is null || instance.ExpiresOnUtc > utcNow)
                        {
                            wasRemoved = true;
                            return;
                        }
                        if (instance.IdleDurationSeconds is not > 0)
                            throw new InvalidOperationException("The instance has no valid projected idle duration.");
                        await batchIdle.HandleAsync(new EnterIdleInstanceDecision(instance.Id, null, null,
                            "The maximum orchestration lifetime expired.")
                        {
                            LifetimeAnchorUtc = instance.ExpiresOnUtc,
                            IdleDurationSeconds = instance.IdleDurationSeconds
                        }, token);
                        wasRemoved = true;
                        processed++;
                    }, cancellationToken, waitForLease: false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (KeyNotFoundException) { wasRemoved = true; }
                catch (Exception error)
                {
                    logger.LogError(error, "Could not expire orchestration instance {InstanceId}.", id);
                    // The state transition may have committed before a later cancellation failed.
                    // Re-check whether this row still occupies a position in the result page.
                    try
                    {
                        var current = await batchInstances.GetById(id, cancellationToken);
                        wasRemoved = current.Status != OrchestrationInstanceStatus.Running ||
                            current.ExpiresOnUtc is null || current.ExpiresOnUtc > utcNow;
                    }
                    catch (KeyNotFoundException) { wasRemoved = true; }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception readError) { logger.LogWarning(readError, "Could not recheck expiration for instance {InstanceId}.", id); }
                }
                if (wasRemoved) removed++;
            }
            // Processed instances disappear from the result. Skip only those still present.
            skip += page.Count - removed;
            if (page.Count < pageSize) break;
        }
        return processed;
    }
}
