namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

internal sealed class DefaultOrchestrationCompensationRecoveryCoordinator : IOrchestrationCompensationRecoveryCoordinator
{
    private const string CompensationTerminalFailureMetadataKey = "Compensation.TerminalFailure";
    private const string DeliveryCommandMetadataKey = "Runtime.Compensation.Delivery.Command";
    private const string DeliveryTerminalFailureMetadataKey = "Runtime.Compensation.Delivery.TerminalFailure";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRemoteCommandDispatcher _dispatcher;
    private readonly IRuntimeStateTransaction _stateTransaction;
    private readonly IOrchestrationInstanceCoordinator _coordinator;

    public DefaultOrchestrationCompensationRecoveryCoordinator(
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IExecutionTransitionRepository transitionRepository,
        IRemoteCommandDispatcher dispatcher,
        IRuntimeStateTransaction stateTransaction,
        IOrchestrationInstanceCoordinator coordinator)
    {
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _stateTransaction = stateTransaction ?? throw new ArgumentNullException(nameof(stateTransaction));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
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
            var handled = false;
            var acquired = await _coordinator.TryExecuteAsync(
                candidate.OrchestrationInstanceId,
                async token => handled = await RecoverCompensationAsync(candidate.Id, token),
                cancellationToken,
                waitForLease: false);
            if (acquired && handled)
            {
                recovered++;
            }
        }

        return recovered;
    }

    private async Task<bool> RecoverCompensationAsync(Id compensationId, CancellationToken cancellationToken)
    {
        var compensation = await _compensationRepository.TryGetById(compensationId, cancellationToken);
        if (compensation is null || !IsPending(compensation.Status))
        {
            return false;
        }

        var instance = await _instanceRepository.GetById(compensation.OrchestrationInstanceId, cancellationToken);
        if (instance.Status != OrchestrationInstanceStatus.Compensating)
        {
            await CancelCompensationAsync(instance, compensation, cancellationToken);
            return true;
        }

        if (!TryReadCommand(compensation, out var command, out var reason))
        {
            await FailCompensationAsync(instance, compensation, "CompensationRecoverySnapshotMissing", reason, cancellationToken);
            return true;
        }

        try
        {
            await _dispatcher.DispatchAsync(command, cancellationToken);
            await CompleteCompensationAsync(instance, compensation, command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RemoteCommandDeliveryUncertainException exception)
        {
            await DeferCompensationAsync(instance, compensation, exception, cancellationToken);
        }
        catch (Exception exception)
        {
            await FailCompensationAsync(instance, compensation, "CompensationRecoveryDispatchFailed", exception.Message, cancellationToken);
        }

        return true;
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
            Id = Id.New(),
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

    private static bool IsPending(string status)
        => string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase);

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
}
