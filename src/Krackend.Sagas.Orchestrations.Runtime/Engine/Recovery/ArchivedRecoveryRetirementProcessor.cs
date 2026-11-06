using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>Retires restored archived backchannels after their last recovery has finished.</summary>
internal sealed class ArchivedRecoveryRetirementProcessor(
    IRuntimeArtifactRepository artifacts,
    IOrchestrationInstanceRepository instances,
    IRuntimeStorageUnitOfWork unitOfWork,
    IIngressRegistry ingress,
    IOptions<OrchestrationTimeoutOptions> timeoutOptions,
    IOptions<OrchestrationCoordinationOptions> coordinationOptions,
    ILogger<ArchivedRecoveryRetirementProcessor> logger)
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        if (!coordinationOptions.Value.Enabled || artifacts is not IArchivedRecoveryRetirementRepository retirement ||
            artifacts is not IArchivedRecoveryBatchRepository batch || instances is not IOrchestrationArtifactActivityBatchReader activity)
        {
            logger.LogDebug("Archived recovery cleanup requires lifecycle fencing and enabled instance coordination.");
            return 0;
        }

        var pageSize = Math.Clamp(timeoutOptions.Value.InstanceLifetimeBatchSize, 1, 1000);
        var skip = 0;
        var retired = 0;
        while (true)
        {
            var page = await retirement.GetArchivedRecoveryCandidates(skip, pageSize, cancellationToken);
            if (page.Count == 0) break;
            var removed = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var active = (await activity.GetActiveArtifactIds(page.Select(x => x.ArtifactId).ToArray(), cancellationToken)).ToHashSet();
                var candidates = page.Where(x => !active.Contains(x.ArtifactId)).ToArray();
                var retiredIds = await TryRetireBatchAsync(candidates, batch, activity, cancellationToken);
                removed = retiredIds.Count;
                retired += removed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not retire an archived recovery backchannel batch."); }
            skip += page.Count - removed;
            if (page.Count < pageSize) break;
        }

        await CleanLocalConnectorsAsync(retirement, pageSize, cancellationToken);
        return retired;
    }

    private async Task<IReadOnlyCollection<Id>> TryRetireBatchAsync(IReadOnlyCollection<ArchivedRecoveryCandidate> candidates,
        IArchivedRecoveryBatchRepository batch, IOrchestrationArtifactActivityBatchReader activity, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return [];
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        using (unitOfWork.DeferAutoSave())
        {
            var fenced = await batch.FenceArchivedRecoveries(candidates, cancellationToken);
            if (fenced.Count == 0) return [];
            await unitOfWork.SaveChanges(cancellationToken);
            var active = (await activity.GetActiveArtifactIds(fenced.Select(x => x.ArtifactId).ToArray(), cancellationToken)).ToHashSet();
            var ready = fenced.Where(x => !active.Contains(x.ArtifactId)).ToArray();
            if (ready.Length == 0) return [];
            var retired = await batch.RetireArchivedRecoveries(ready, cancellationToken);
            if (retired.Count == 0) return [];
            await unitOfWork.SaveChanges(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return retired;
        }
    }

    private async Task CleanLocalConnectorsAsync(IArchivedRecoveryRetirementRepository retirement, int pageSize, CancellationToken cancellationToken)
    {
        if (ingress is not IArchivedRecoveryIngressCleanup cleanup) return;
        var ids = cleanup.GetLocalArtifactIds().Select(value => System.Ulid.TryParse(value, out var parsed) ? new Id?(new Id(parsed)) : null)
            .Where(x => x.HasValue).Select(x => x.Value).Distinct().ToArray();
        foreach (var page in ids.Chunk(pageSize))
        {
            var retiredIds = await retirement.GetRetiredArchivedIds(page, cancellationToken);
            foreach (var id in retiredIds)
            {
                try { await cleanup.ShutDownIfArchivedAsync(id.ToString(), cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { logger.LogWarning(exception, "Could not close local archived backchannel for artifact {ArtifactId}.", id); }
            }
        }
    }
}
