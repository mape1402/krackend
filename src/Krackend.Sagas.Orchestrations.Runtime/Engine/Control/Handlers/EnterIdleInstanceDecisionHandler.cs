using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;

/// <summary>
/// Enters the fixed, durable reconciliation window before an unresolved saga is dead-lettered.
/// </summary>
internal sealed class EnterIdleInstanceDecisionHandler : IDecisionHandler<EnterIdleInstanceDecision>
{
    internal static readonly TimeSpan IdleWindow = TimeSpan.FromMinutes(20);
    private const string IdleReasonMetadataKey = "Idle.Reason";

    private readonly IOrchestrationInstanceRepository _instances;
    private readonly IExecutionTransitionRepository _transitions;
    private readonly IOrchestrationDispatchCancellationCoordinator _cancellations;
    private readonly IRuntimeStateTransaction _transaction;

    public EnterIdleInstanceDecisionHandler(
        IOrchestrationInstanceRepository instances,
        IExecutionTransitionRepository transitions,
        IOrchestrationDispatchCancellationCoordinator cancellations,
        IRuntimeStateTransaction transaction = null)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        _cancellations = cancellations ?? throw new ArgumentNullException(nameof(cancellations));
        _transaction = transaction;
    }

    public async Task HandleAsync(EnterIdleInstanceDecision decision, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var instance = await _instances.GetById(decision.InstanceId, cancellationToken);
        if (IsTerminal(instance.Status) || instance.Status == OrchestrationInstanceStatus.Idle)
        {
            return;
        }

        var previousStatus = instance.Status;
        var reason = string.IsNullOrWhiteSpace(decision.Reason)
            ? "No executable retry remains; awaiting a reconciliable callback."
            : decision.Reason.Trim();

        instance.Status = OrchestrationInstanceStatus.Idle;
        instance.IdleSinceUtc = now;
        instance.IdleExpiresOnUtc = now.Add(IdleWindow);
        instance.WaitingSinceUtc = null;
        instance.LastUpdatedOnUtc = now;
        instance.ErrorSummary = reason;
        instance.DispatchCancellationPending = true;
        instance.Metadata[IdleReasonMetadataKey] = JsonValue.Create(reason);

        await PersistAsync(async token =>
        {
            await _instances.Update(instance, token);
            await _transitions.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = decision.TaskExecutionId,
                TransitionType = "InstanceIdle",
                FromStatus = previousStatus.ToString(),
                ToStatus = OrchestrationInstanceStatus.Idle.ToString(),
                OccurredOnUtc = now,
                Message = reason,
                ProducedBy = nameof(EnterIdleInstanceDecisionHandler)
            }, token);
        }, cancellationToken);

        await _cancellations.CancelPendingAsync(
            instance,
            "Orchestration instance entered its reconciliation idle window.",
            cancellationToken);
    }

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Failed
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.Aborted;

    private Task PersistAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken)
        => _transaction is null ? mutation(cancellationToken) : _transaction.ExecuteAsync(mutation, cancellationToken);
}
