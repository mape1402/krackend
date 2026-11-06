using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class OrchestrationLifetimeProcessor(
    IRuntimeArtifactRepository artifacts,
    IRuntimeArtifactSerializer serializer,
    IOrchestrationInstanceRepository instances,
    IOrchestrationInstanceCoordinator coordinator,
    IDecisionHandler<EnterIdleInstanceDecision> idle,
    IOptions<OrchestrationTimeoutOptions> options,
    ILogger<OrchestrationLifetimeProcessor> logger)
{
    public async Task<int> ProcessAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (instances is IOrchestrationLifetimeScanRepository scan)
            return await ProcessGlobalAsync(scan, utcNow, cancellationToken);
        if (instances is not IOrchestrationLifetimeCandidateRepository candidates)
        {
            logger.LogWarning("The instance repository does not support orchestration lifetime scans.");
            return 0;
        }

        var processed = 0;
        var pageSize = Math.Clamp(options.Value.InstanceLifetimeBatchSize, 1, 1000);
        await foreach (var runtimeArtifact in ReadPoliciesAsync(pageSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = ReadPolicy(runtimeArtifact);
            if (policy is null) continue;
            var cutoff = utcNow.AddSeconds(-policy.MaxSeconds);
            var skip = 0;
            while (true)
            {
                var page = await candidates.GetLifetimeCandidates(runtimeArtifact.Id, cutoff, skip, pageSize, cancellationToken);
                if (page.Count == 0) break;
                var removedFromCandidates = 0;
                foreach (var candidate in page)
                {
                    var (expired, removed) = await ProcessCandidateAsync(candidate, runtimeArtifact.Id, policy, cutoff, false, cancellationToken);
                    if (expired) processed++;
                    if (removed) removedFromCandidates++;
                }
                // Successfully transitioned rows disappear from the query, so retain an offset only for rows left behind.
                skip += page.Count - removedFromCandidates;
                if (page.Count < pageSize) break;
            }
        }
        return processed;
    }

    private async Task<int> ProcessGlobalAsync(IOrchestrationLifetimeScanRepository scan, DateTime utcNow, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(options.Value.InstanceLifetimeBatchSize, 1, 1000);
        var policies = new Dictionary<Id, LifetimePolicy>();
        await foreach (var artifact in ReadPoliciesAsync(pageSize, cancellationToken))
        {
            var policy = ReadPolicy(artifact);
            if (policy is not null) policies[artifact.Id] = policy;
        }
        if (policies.Count == 0) return 0;

        var earliestCutoff = utcNow.AddSeconds(-policies.Values.Min(x => x.MaxSeconds));
        var processed = 0;
        var skip = 0;
        while (true)
        {
            var page = await scan.GetLifetimeCandidatePage(earliestCutoff, skip, pageSize, cancellationToken);
            if (page.Count == 0) break;
            var removedFromCandidates = 0;
            foreach (var candidate in page)
            {
                if (!policies.TryGetValue(candidate.ArtifactId, out var policy)) continue;
                var policyCutoff = utcNow.AddSeconds(-policy.MaxSeconds);
                if (candidate.StartedOnUtc > policyCutoff) continue;
                var (expired, removed) = await ProcessCandidateAsync(candidate.InstanceId, candidate.ArtifactId, policy,
                    policyCutoff, true, cancellationToken);
                if (expired) processed++;
                if (removed) removedFromCandidates++;
            }
            skip += page.Count - removedFromCandidates;
            if (page.Count < pageSize) break;
        }
        return processed;
    }

    private async Task<(bool Expired, bool Removed)> ProcessCandidateAsync(
        Id candidate, Id artifactId, LifetimePolicy policy, DateTime cutoff, bool globalScan, CancellationToken cancellationToken)
    {
        var removed = false;
        var expired = false;
        try
        {
            await coordinator.TryExecuteAsync(candidate, async token =>
            {
                var current = await instances.GetById(candidate, token);
                if (current.Status != OrchestrationInstanceStatus.Running || current.RuntimeOrchestrationArtifactId != artifactId)
                {
                    removed = current.Status != OrchestrationInstanceStatus.Running || !globalScan;
                    return;
                }
                var anchor = RecoveryAnchor(current) ?? current.StartedOnUtc;
                if (anchor > cutoff) return;
                await idle.HandleAsync(new EnterIdleInstanceDecision(current.Id, null, null,
                    $"The maximum orchestration lifetime of {policy.MaxSeconds} seconds expired.")
                    { LifetimeAnchorUtc = anchor, IdleDurationSeconds = policy.IdleSeconds }, token);
                expired = true;
                removed = true;
            }, cancellationToken, waitForLease: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Could not expire orchestration instance {InstanceId}.", candidate);
            try
            {
                var current = await instances.GetById(candidate, cancellationToken);
                removed = current.Status != OrchestrationInstanceStatus.Running ||
                    (!globalScan && current.RuntimeOrchestrationArtifactId != artifactId);
            }
            catch (KeyNotFoundException) { removed = true; }
        }
        return (expired, removed);
    }

    private LifetimePolicy ReadPolicy(RuntimeArtifactLifetimePolicyItem runtimeArtifact)
    {
        try
        {
            var artifact = serializer.Deserialize(runtimeArtifact.ArtifactPayload.ToJsonString());
            var maxSeconds = artifact.MaxInstanceDurationSeconds ?? options.Value.DefaultInstanceLifetimeSeconds;
            var idleSeconds = artifact.IdleDurationSeconds ?? options.Value.DefaultIdleDurationSeconds;
            if (maxSeconds < 0) logger.LogError("The maximum instance lifetime for artifact {ArtifactId} must be non-negative.", runtimeArtifact.Id);
            else if (maxSeconds > 0 && idleSeconds <= 0) logger.LogError("The idle duration for artifact {ArtifactId} must be positive.", runtimeArtifact.Id);
            else if (maxSeconds > 0) return new LifetimePolicy(maxSeconds, idleSeconds);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read the lifetime policy for artifact {ArtifactId}.", runtimeArtifact.Id);
        }
        return null;
    }

    private sealed record LifetimePolicy(int MaxSeconds, int IdleSeconds);

    private async IAsyncEnumerable<RuntimeArtifactLifetimePolicyItem> ReadPoliciesAsync(
        int pageSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (artifacts is not IRuntimeArtifactLifetimePolicyRepository policyRepository)
        {
            logger.LogWarning("The artifact repository does not support paged lifetime policies; falling back to loading active artifacts.");
            foreach (var artifact in await artifacts.GetAll(cancellationToken))
                if (artifact.IsActive) yield return new(artifact.Id, artifact.ArtifactPayload);
            yield break;
        }

        var skip = 0;
        while (true)
        {
            var page = await policyRepository.GetLifetimePolicyPage(skip, pageSize, cancellationToken);
            foreach (var policy in page) yield return policy;
            if (page.Count < pageSize) yield break;
            skip += page.Count;
        }
    }

    private static DateTime? RecoveryAnchor(OrchestrationInstance instance)
    {
        if (!instance.Metadata.TryGetValue("Recovery.ReplayRequestedOnUtc", out JsonNode value) || value is null) return null;
        try { return value.GetValue<DateTime>(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { return null; }
    }
}
