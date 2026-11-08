using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>Projects legacy policies once, caching each artifact within the backfill run.</summary>
internal sealed class OrchestrationLifetimeBackfill(
    IOrchestrationInstanceRepository instances,
    IRuntimeArtifactRepository artifacts,
    IRuntimeArtifactSerializer serializer,
    IOrchestrationInstanceCoordinator coordinator,
    IOptions<OrchestrationTimeoutOptions> options,
    ILogger<OrchestrationLifetimeBackfill> logger,
    IServiceScopeFactory scopes = null)
{
    public async Task<bool> ProcessAsync(CancellationToken cancellationToken = default)
    {
        if (instances is not IOrchestrationExpirationRepository repository)
        {
            logger.LogWarning("The instance repository does not support legacy lifetime projection.");
            return false;
        }
        var policies = new Dictionary<Id, (int Maximum, int Idle)>();
        var unavailableArtifacts = new HashSet<Id>();
        var pageSize = Math.Clamp(options.Value.InstanceLifetimeBatchSize, 1, 1000);
        var skip = 0;
        var incomplete = false;
        while (true)
        {
            var page = await repository.GetUnprojectedInstances(skip, pageSize, cancellationToken);
            if (page.Count == 0) break;
            using var batchScope = scopes?.CreateScope();
            var batchInstances = batchScope?.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>() ?? instances;
            var batchCoordinator = batchScope?.ServiceProvider.GetRequiredService<IOrchestrationInstanceCoordinator>() ?? coordinator;
            var removed = 0;
            foreach (var id in page)
            {
                var done = false;
                try
                {
                    await batchCoordinator.TryExecuteAsync(id, async token =>
                    {
                        var instance = await batchInstances.GetById(id, token);
                        if (instance.MaxInstanceDurationSeconds is not null || !IsBackfillable(instance.Status))
                        {
                            done = true;
                            return;
                        }
                        if (!policies.TryGetValue(instance.RuntimeOrchestrationArtifactId, out var policy))
                        {
                            if (unavailableArtifacts.Contains(instance.RuntimeOrchestrationArtifactId)) return;
                            try
                            {
                                var artifact = await artifacts.GetById(instance.RuntimeOrchestrationArtifactId, token);
                                var definition = serializer.Deserialize(artifact.ArtifactPayload.ToJsonString());
                                policy = InstanceLifetimePolicy.Resolve(definition, options.Value);
                                policies.Add(instance.RuntimeOrchestrationArtifactId, policy);
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                            catch (Exception error)
                            {
                                unavailableArtifacts.Add(instance.RuntimeOrchestrationArtifactId);
                                logger.LogError(error, "Cannot read lifetime policy for artifact {ArtifactId}.", instance.RuntimeOrchestrationArtifactId);
                                return;
                            }
                        }
                        var anchor = InstanceLifetimePolicy.ReadRecoveryAnchor(instance) ?? instance.StartedOnUtc;
                        InstanceLifetimePolicy.Project(instance, policy.Maximum, policy.Idle, anchor);
                        PreserveIdleDeadlineAnchor(instance, anchor);
                        await batchInstances.Update(instance, token);
                        done = true;
                    }, cancellationToken, waitForLease: false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (KeyNotFoundException error) { logger.LogWarning(error, "Cannot project lifetime for instance {InstanceId}.", id); }
                catch (Exception error) { logger.LogError(error, "Cannot project lifetime for instance {InstanceId}.", id); }
                if (done) removed++; else incomplete = true;
            }
            skip += page.Count - removed;
            if (page.Count < pageSize) break;
        }
        return !incomplete;
    }

    private static bool IsBackfillable(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Created or OrchestrationInstanceStatus.Running or
            OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.DeadLettered;

    private static void PreserveIdleDeadlineAnchor(OrchestrationInstance instance, DateTime oldAnchor)
    {
        if (instance.ExpiresOnUtc is not { } deadline || instance.Metadata is null ||
            !instance.Metadata.TryGetValue("Lifetime.ExpirationAnchorOnUtc", out var previous) || previous is null ||
            !instance.Metadata.ContainsKey("Lifetime.IdleDeadlineOnUtc")) return;
        try
        {
            if (previous.GetValue<DateTime>() == oldAnchor)
                instance.Metadata["Lifetime.ExpirationAnchorOnUtc"] = System.Text.Json.Nodes.JsonValue.Create(deadline);
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { }
    }
}
