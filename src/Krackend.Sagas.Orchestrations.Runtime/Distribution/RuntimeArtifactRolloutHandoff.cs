using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>Applies the trigger part of a projected rollout inside its storage transaction.</summary>
public static class RuntimeArtifactRolloutHandoff
{
    /// <summary>Checks that the rollout still owns its claims and switches its affected triggers.</summary>
    public static async Task ApplyAsync(
        RuntimeArtifactProjectionRequest request,
        IRuntimeIngressConfigurationRepository ingress,
        IRuntimeTriggerClaimRepository claims,
        CancellationToken cancellationToken = default)
    {
        var replacements = request.RolloutReplacements ?? [];
        var targetKeys = (request.RollbackTargetTriggerKeys ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (replacements.Length == 0 && targetKeys.Length == 0) return;
        if (ingress is not IRuntimeSelectiveTriggerIngressRepository selective || claims is null)
            throw new IngressProjectionConfigurationException("The runtime cannot safely complete the rollout trigger handoff.");
        var identities = request.RolloutTriggerIdentities ?? [];
        if (identities.Length == 0)
            throw new IngressProjectionConfigurationException("The rollout trigger handoff has no trigger identities.");

        var artifactId = new Id(Ulid.Parse(request.ArtifactId));
        var claim = await claims.ClaimAsync(identities, artifactId, [], cancellationToken);
        if (!claim.Succeeded)
            throw new IngressProjectionConfigurationException("The rollout no longer owns its trigger claims.");

        foreach (var replacement in replacements)
        {
            var sourceId = new Id(Ulid.Parse(replacement.ArtifactId));
            await selective.DeactivateTriggersAsync(sourceId, replacement.ConfigurationKeys ?? [], cancellationToken);
            if ((replacement.ReleaseTriggerIdentities?.Length ?? 0) > 0)
            {
                if (claims is not IRuntimeSelectiveTriggerClaimRepository selectiveClaims)
                    throw new IngressProjectionConfigurationException("The runtime cannot safely release rollback trigger claims.");
                await selectiveClaims.ReleaseOwnedAsync(sourceId, replacement.ReleaseTriggerIdentities, cancellationToken);
            }
        }

        if (targetKeys.Length > 0)
        {
            if (ingress is not IRuntimeReactivatableTriggerIngressRepository reactivatable)
                throw new IngressProjectionConfigurationException("The runtime storage cannot reactivate rollback triggers.");
            var found = await reactivatable.ActivateTriggersAsync(artifactId, targetKeys, cancellationToken);
            if (found != targetKeys.Length)
                throw new IngressProjectionConfigurationException("A rollback target trigger configuration is missing or cannot process messages.");
        }
    }
}
