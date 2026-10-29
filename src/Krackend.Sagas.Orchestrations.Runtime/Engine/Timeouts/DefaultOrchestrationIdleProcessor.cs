using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class DefaultOrchestrationIdleProcessor : IOrchestrationIdleProcessor
{
    private readonly IOrchestrationInstanceRepository _instances;
    private readonly IOrchestrationIdleInstanceRepository _idleInstances;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IDecisionHandler<DeadLetterInstanceDecision> _deadLetter;

    public DefaultOrchestrationIdleProcessor(
        IOrchestrationInstanceRepository instances,
        IOrchestrationInstanceCoordinator coordinator,
        IDecisionHandler<DeadLetterInstanceDecision> deadLetter)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _idleInstances = instances as IOrchestrationIdleInstanceRepository;
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _deadLetter = deadLetter ?? throw new ArgumentNullException(nameof(deadLetter));
    }

    public async Task<int> ProcessExpiredAsync(DateTime utcNow, int take = 100, CancellationToken cancellationToken = default)
    {
        if (_idleInstances is null)
        {
            return 0;
        }

        var processed = 0;
        var candidates = await _idleInstances.GetIdleExpiringOnOrBefore(utcNow, Math.Max(1, take), cancellationToken);
        foreach (var candidate in candidates)
        {
            var applied = false;
            await _coordinator.TryExecuteAsync(candidate.Id, async token =>
            {
                var current = await _instances.GetById(candidate.Id, token);
                if (current.Status != OrchestrationInstanceStatus.Idle ||
                    current.IdleExpiresOnUtc is null ||
                    current.IdleExpiresOnUtc > utcNow)
                {
                    return;
                }

                await _deadLetter.HandleAsync(new DeadLetterInstanceDecision(
                    current.Id,
                    null,
                    null,
                    "The 20-minute reconciliation idle window expired without a valid callback."), token);
                applied = true;
            }, cancellationToken, waitForLease: false);

            if (applied)
            {
                processed++;
            }
        }

        return processed;
    }
}
