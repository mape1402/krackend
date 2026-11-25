namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Microsoft.Extensions.DependencyInjection;

internal sealed class DefaultOrchestrationCompensationRecoveryCoordinator : IOrchestrationCompensationRecoveryCoordinator
{
    private const string CompensationTerminalFailureMetadataKey = "Compensation.TerminalFailure";
    private const string DeliveryCommandMetadataKey = "Runtime.Compensation.Delivery.Command";
    private const string DeliveryTerminalFailureMetadataKey = "Runtime.Compensation.Delivery.TerminalFailure";
    private const string DeliveryClaimIdMetadataKey = "Runtime.Compensation.Delivery.ClaimId";
    private const string DeliveryClaimExpiresOnUtcMetadataKey = "Runtime.Compensation.Delivery.ClaimExpiresOnUtc";
    private static readonly TimeSpan DeliveryClaimDuration = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRemoteCommandDispatcher _dispatcher;
    private readonly IRuntimeStateTransaction _stateTransaction;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IServiceScopeFactory _scopeFactory;

    public DefaultOrchestrationCompensationRecoveryCoordinator(
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRemoteCommandDispatcher dispatcher,
        IRuntimeStateTransaction stateTransaction,
        IOrchestrationInstanceCoordinator coordinator,
        IServiceScopeFactory scopeFactory = null)
    {
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _stateTransaction = stateTransaction ?? throw new ArgumentNullException(nameof(stateTransaction));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _scopeFactory = scopeFactory;
    }

    public async Task<int> RecoverPendingCompensationsAsync(int take, CancellationToken cancellationToken = default)
    {
        var candidates = (await _compensationRepository.GetPending(cancellationToken))
            .OrderBy(x => x.StartedOnUtc)
            .Take(Math.Max(1, take))
            .ToArray();
        var recovered = 0;

        foreach (var candidate in candidates)
        {
            if (await RecoverCompensationAsync(candidate.Id, cancellationToken))
            {
                recovered++;
            }
        }

        return recovered;
    }

    public async Task<bool> RecoverCompensationAsync(Id compensationId, CancellationToken cancellationToken = default)
    {
        var compensation = await _compensationRepository.TryGetById(compensationId, cancellationToken);
        if (compensation is null)
        {
            return false;
        }

        if (_coordinator is IOrchestrationPostLeaseWork postLeaseWork &&
            postLeaseWork.IsWithinMutation(compensation.OrchestrationInstanceId))
        {
            await postLeaseWork.ExecuteIsolatedAfterLeaseReleaseAsync(
                compensation.OrchestrationInstanceId,
                async (services, token) =>
                {
                    var recovery = services.GetRequiredService<IOrchestrationCompensationRecoveryCoordinator>();
                    await recovery.RecoverCompensationAsync(compensationId, token);
                },
                cancellationToken);
            return true;
        }

        return await RecoverCompensationOutsideLeaseAsync(compensation, cancellationToken);
    }

    private async Task<bool> RecoverCompensationOutsideLeaseAsync(
        CompensationExecution candidate,
        CancellationToken cancellationToken)
    {
        var claim = await TryClaimAsync(candidate, cancellationToken);
        if (!claim.Acquired)
        {
            if (claim.ResumeCompensation)
            {
                await ResumeCompensationAsync(candidate.OrchestrationInstanceId, cancellationToken);
            }
            return claim.Handled;
        }

        DispatchOutcome outcome;
        try
        {
            await _dispatcher.DispatchAsync(claim.Command, cancellationToken);
            outcome = new DispatchOutcome(DispatchOutcomeKind.Completed, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RemoteCommandDeliveryUncertainException exception)
        {
            outcome = new DispatchOutcome(DispatchOutcomeKind.Deferred, exception);
        }
        catch (Exception exception)
        {
            outcome = new DispatchOutcome(DispatchOutcomeKind.Failed, exception);
        }

        var finalization = await FinalizeClaimAsync(claim, outcome, cancellationToken);
        if (finalization.ResumeCompensation)
        {
            await ResumeCompensationAsync(candidate.OrchestrationInstanceId, cancellationToken);
        }

        return finalization.Handled;
    }

    private async Task<CompensationClaim> TryClaimAsync(
        CompensationExecution candidate,
        CancellationToken cancellationToken)
    {
        var claim = CompensationClaim.NotAcquired;
        var acquired = await _coordinator.TryExecuteAsync(
            candidate.OrchestrationInstanceId,
            async token =>
            {
                var compensation = await _compensationRepository.TryGetById(candidate.Id, token);
                if (compensation is null || !IsRecoverable(compensation.Status))
                {
                    return;
                }

                var instance = await _instanceRepository.GetById(compensation.OrchestrationInstanceId, token);
                if (instance.Status != OrchestrationInstanceStatus.Compensating)
                {
                    await CancelCompensationAsync(instance, compensation, token);
                    claim = CompensationClaim.AlreadyHandled;
                    return;
                }

                if (!TryReadCommand(compensation, out var command, out var reason))
                {
                    await FailCompensationAsync(
                        instance,
                        compensation,
                        "CompensationRecoverySnapshotMissing",
                        reason,
                        token);
                    claim = CompensationClaim.AlreadyHandledWithResume;
                    return;
                }

                var now = DateTime.UtcNow;
                if (string.Equals(compensation.Status, "Dispatching", StringComparison.OrdinalIgnoreCase) &&
                    TryReadClaimExpiry(compensation, out var claimExpiresOnUtc) &&
                    claimExpiresOnUtc > now)
                {
                    return;
                }

                var claimId = Id.NewDistributed().ToString();
                compensation.Status = "Dispatching";
                compensation.Metadata[DeliveryClaimIdMetadataKey] = JsonValue.Create(claimId);
                compensation.Metadata[DeliveryClaimExpiresOnUtcMetadataKey] = JsonValue.Create(now.Add(DeliveryClaimDuration));
                await _stateTransaction.ExecuteAsync(
                    token => _compensationRepository.Update(compensation, token),
                    token);
                claim = new CompensationClaim(true, false, false, compensation.Id,
                    compensation.OrchestrationInstanceId, claimId, command);
            },
            cancellationToken,
            waitForLease: false);

        return acquired ? claim : CompensationClaim.NotAcquired;
    }

    private async Task<CompensationFinalization> FinalizeClaimAsync(
        CompensationClaim claim,
        DispatchOutcome outcome,
        CancellationToken cancellationToken)
    {
        var result = CompensationFinalization.NotHandled;
        var acquired = await _coordinator.TryExecuteAsync(
            claim.InstanceId,
            async token =>
            {
                var compensation = await _compensationRepository.TryGetById(claim.CompensationId, token);
                if (compensation is null || !OwnsClaim(compensation, claim.ClaimId))
                {
                    return;
                }

                var instance = await _instanceRepository.GetById(claim.InstanceId, token);
                ClearClaim(compensation);
                switch (outcome.Kind)
                {
                    case DispatchOutcomeKind.Completed:
                        await CompleteCompensationAsync(instance, compensation, claim.Command, token);
                        result = CompensationFinalization.HandledWithResume;
                        break;
                    case DispatchOutcomeKind.Deferred:
                        await DeferCompensationAsync(instance, compensation, outcome.Exception!, token);
                        result = CompensationFinalization.HandledNoResume;
                        break;
                    default:
                        await FailCompensationAsync(
                            instance,
                            compensation,
                            "CompensationRecoveryDispatchFailed",
                            outcome.Exception?.Message ?? "Compensation dispatch failed.",
                            token);
                        result = CompensationFinalization.HandledWithResume;
                        break;
                }
            },
            cancellationToken,
            waitForLease: false);

        return acquired ? result : CompensationFinalization.NotHandled;
    }

    private async Task ResumeCompensationAsync(Id instanceId, CancellationToken cancellationToken)
    {
        if (_scopeFactory is null)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var instances = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = await instances.GetById(instanceId, cancellationToken);
        if (instance.Status != OrchestrationInstanceStatus.Compensating)
        {
            return;
        }

        var engine = scope.ServiceProvider.GetRequiredService<ISagaEngine>();
        await engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = instance.CurrentStageKey,
                CurrentTasks = string.IsNullOrWhiteSpace(instance.CurrentTaskKey) ? [] : [instance.CurrentTaskKey],
                CorrelationId = instance.CorrelationId
            },
            Payload = instance.SnapshotPayload?.DeepClone()
        }, cancellationToken);
    }

    private async Task CompleteCompensationAsync(
        OrchestrationInstance instance,
        CompensationExecution compensation,
        RemoteCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        compensation.Status = "Completed";
        compensation.CompletedOnUtc = now;
        compensation.FailedOnUtc = null;
        compensation.ErrorMessage = null;
        compensation.Metadata.Remove("Runtime.Compensation.Delivery.LastFailure");

        await _stateTransaction.ExecuteAsync(async token =>
        {
            await _compensationRepository.Update(compensation, token);
            await _transitionRepository.Create(CreateTransition(
                instance,
                compensation,
                command,
                "CompensationDispatchRecovered",
                "Pending",
                "Completed",
                now,
                "Pending compensation delivery recovered."), token);
        }, cancellationToken);
    }

    private async Task DeferCompensationAsync(
        OrchestrationInstance instance,
        CompensationExecution compensation,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        compensation.Status = "Pending";
        compensation.Metadata["Runtime.Compensation.Delivery.LastFailure"] = JsonValue.Create(exception.Message);
        compensation.Metadata["Runtime.Compensation.Delivery.LastAttemptOnUtc"] = JsonValue.Create(now);

        await _stateTransaction.ExecuteAsync(async token =>
        {
            await _compensationRepository.Update(compensation, token);
            await _transitionRepository.Create(CreateTransition(
                instance,
                compensation,
                null,
                "CompensationDispatchRecoveryDeferred",
                "Pending",
                "Pending",
                now,
                exception.Message), token);
        }, cancellationToken);
    }

    private async Task FailCompensationAsync(
        OrchestrationInstance instance,
        CompensationExecution compensation,
        string errorCode,
        string message,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var terminalFailure = IsTerminalFailure(compensation);
        ClearClaim(compensation);
        compensation.Status = "Failed";
        compensation.FailedOnUtc = now;
        compensation.ErrorMessage = message;
        compensation.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
        compensation.Metadata["ExecutionErrorMessage"] = JsonValue.Create(message);

        if (terminalFailure)
        {
            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = now;
            instance.ErrorSummary = message;
            instance.LastUpdatedOnUtc = now;
            instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);
        }

        await _stateTransaction.ExecuteAsync(async token =>
        {
            await _compensationRepository.Update(compensation, token);
            if (terminalFailure)
            {
                await _instanceRepository.Update(instance, token);
            }

            await _transitionRepository.Create(CreateTransition(
                instance,
                compensation,
                null,
                "CompensationDispatchRecoveryFailed",
                "Pending",
                "Failed",
                now,
                message), token);
        }, cancellationToken);
    }

    private async Task CancelCompensationAsync(
        OrchestrationInstance instance,
        CompensationExecution compensation,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        ClearClaim(compensation);
        compensation.Status = "Cancelled";
        compensation.FailedOnUtc = now;
        compensation.ErrorMessage = "Compensation delivery is no longer actionable for the orchestration instance.";

        await _stateTransaction.ExecuteAsync(async token =>
        {
            await _compensationRepository.Update(compensation, token);
            await _transitionRepository.Create(CreateTransition(
                instance,
                compensation,
                null,
                "CompensationDispatchRecoverySuppressed",
                "Pending",
                "Cancelled",
                now,
                compensation.ErrorMessage), token);
        }, cancellationToken);
    }

    private static ExecutionTransition CreateTransition(
        OrchestrationInstance instance,
        CompensationExecution compensation,
        RemoteCommand command,
        string transitionType,
        string fromStatus,
        string toStatus,
        DateTime occurredOnUtc,
        string message)
        => new()
        {
            Id = Id.NewDistributed(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = TryParseId(command?.StageExecutionId),
            TaskExecutionId = TryParseId(command?.TaskExecutionId) ?? compensation.SourceTaskExecutionId,
            TransitionType = transitionType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            OccurredOnUtc = occurredOnUtc,
            Message = message,
            ProducedBy = nameof(DefaultOrchestrationCompensationRecoveryCoordinator)
        };

    private static bool IsRecoverable(string status)
        => string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Dispatching", StringComparison.OrdinalIgnoreCase);

    private static bool OwnsClaim(CompensationExecution compensation, string claimId)
        => string.Equals(compensation.Status, "Dispatching", StringComparison.OrdinalIgnoreCase) &&
           compensation.Metadata.TryGetValue(DeliveryClaimIdMetadataKey, out var value) &&
           value is not null &&
           string.Equals(value.GetValue<string>(), claimId, StringComparison.Ordinal);

    private static bool TryReadClaimExpiry(
        CompensationExecution compensation,
        out DateTime expiresOnUtc)
    {
        expiresOnUtc = default;
        if (!compensation.Metadata.TryGetValue(DeliveryClaimExpiresOnUtcMetadataKey, out var value) || value is null)
        {
            return false;
        }

        try
        {
            expiresOnUtc = value.GetValue<DateTime>();
            return true;
        }
        catch (InvalidOperationException)
        {
            return DateTime.TryParse(value.ToString(), out expiresOnUtc);
        }
    }

    private static void ClearClaim(CompensationExecution compensation)
    {
        compensation.Metadata.Remove(DeliveryClaimIdMetadataKey);
        compensation.Metadata.Remove(DeliveryClaimExpiresOnUtcMetadataKey);
    }

    private static bool IsTerminalFailure(CompensationExecution compensation)
        => compensation.Metadata.TryGetValue(DeliveryTerminalFailureMetadataKey, out var value) &&
           value is not null &&
           value.GetValueKind() == System.Text.Json.JsonValueKind.True;

    private static bool TryReadCommand(
        CompensationExecution compensation,
        out RemoteCommand command,
        out string reason)
    {
        command = null!;
        reason = $"Compensation '{compensation.Id}' does not contain a recoverable delivery snapshot.";
        if (!compensation.Metadata.TryGetValue(DeliveryCommandMetadataKey, out var commandNode) || commandNode is null)
        {
            return false;
        }

        try
        {
            command = commandNode.Deserialize<RemoteCommand>(SerializerOptions)!;
            if (command is not null)
            {
                return true;
            }
        }
        catch (JsonException)
        {
            reason = $"Compensation '{compensation.Id}' contains an invalid durable delivery snapshot.";
        }

        return false;
    }

    private static Id? TryParseId(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Ulid.TryParse(value, out var parsed))
        {
            return new Id(parsed);
        }

        return null;
    }

    private sealed record CompensationClaim(
        bool Acquired,
        bool Handled,
        bool ResumeCompensation,
        Id CompensationId,
        Id InstanceId,
        string ClaimId,
        RemoteCommand Command)
    {
        public static CompensationClaim NotAcquired { get; } = new(false, false, false, default, default, null, null);
        public static CompensationClaim AlreadyHandled { get; } = new(false, true, false, default, default, null, null);
        public static CompensationClaim AlreadyHandledWithResume { get; } = new(false, true, true, default, default, null, null);
    }

    private enum DispatchOutcomeKind
    {
        Completed,
        Deferred,
        Failed
    }

    private sealed record DispatchOutcome(DispatchOutcomeKind Kind, Exception Exception);

    private sealed record CompensationFinalization(bool Handled, bool ResumeCompensation)
    {
        public static CompensationFinalization NotHandled { get; } = new(false, false);
        public static CompensationFinalization HandledNoResume { get; } = new(true, false);
        public static CompensationFinalization HandledWithResume { get; } = new(true, true);
    }
}
