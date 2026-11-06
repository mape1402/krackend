using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.Options;

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
    private readonly IRuntimeArtifactResolver _artifacts;
    private readonly IOptions<OrchestrationTimeoutOptions> _options;

    public EnterIdleInstanceDecisionHandler(
        IOrchestrationInstanceRepository instances,
        IExecutionTransitionRepository transitions,
        IOrchestrationDispatchCancellationCoordinator cancellations,
        IRuntimeStateTransaction transaction = null,
        IRuntimeArtifactResolver artifacts = null,
        IOptions<OrchestrationTimeoutOptions> options = null)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        _cancellations = cancellations ?? throw new ArgumentNullException(nameof(cancellations));
        _transaction = transaction;
        _artifacts = artifacts;
        _options = options;
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
        var configuredIdleSeconds = decision.IdleDurationSeconds;
        if (!configuredIdleSeconds.HasValue && _artifacts is not null)
            configuredIdleSeconds = (await _artifacts.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken)).Artifact.IdleDurationSeconds;
        var idleSeconds = configuredIdleSeconds ?? _options?.Value.DefaultIdleDurationSeconds ?? (int)IdleWindow.TotalSeconds;
        if (idleSeconds <= 0) throw new InvalidOperationException("The idle duration must be positive.");
        var reason = string.IsNullOrWhiteSpace(decision.Reason)
            ? "No executable retry remains; awaiting a reconciliable callback."
            : decision.Reason.Trim();

        instance.Status = OrchestrationInstanceStatus.Idle;
        instance.IdleSinceUtc = now;
        var idleExpiresOnUtc = now.AddSeconds(idleSeconds);
        if (decision.LifetimeAnchorUtc is { } anchor)
        {
            const string anchorKey = "Lifetime.ExpirationAnchorOnUtc";
            const string deadlineKey = "Lifetime.IdleDeadlineOnUtc";
            if (ReadUtc(instance.Metadata, anchorKey) == anchor && ReadUtc(instance.Metadata, deadlineKey) is { } existingDeadline)
                idleExpiresOnUtc = existingDeadline;
            else
            {
                instance.Metadata[anchorKey] = JsonValue.Create(anchor);
                instance.Metadata[deadlineKey] = JsonValue.Create(idleExpiresOnUtc);
            }
        }
        instance.IdleExpiresOnUtc = idleExpiresOnUtc;
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

    private static DateTime? ReadUtc(Dictionary<string, JsonNode> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var node) || node is null) return null;
        try { return node.GetValue<DateTime>(); }
        catch (Exception error) when (error is InvalidOperationException or FormatException) { return null; }
    }

    private Task PersistAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken)
        => _transaction is null ? mutation(cancellationToken) : _transaction.ExecuteAsync(mutation, cancellationToken);
}
