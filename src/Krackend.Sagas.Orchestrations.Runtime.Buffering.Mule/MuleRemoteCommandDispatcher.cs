using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal sealed class MuleRemoteCommandDispatcher : IDurableRemoteCommandDispatcher
    {
        private readonly IMuleClient _muleClient;
        private readonly IMuleStorage _muleStorage;
        private readonly IMuleSerializer _muleSerializer;
        private readonly IMuleCommitNotifier _commitNotifier;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MuleRemoteCommandDispatcher> _logger;
        private readonly ITaskDispatchRepository? _dispatchRepository;
        private readonly IOrchestrationDispatchCancellationRecoveryCoordinator? _cancellationRecoveryCoordinator;
        private readonly IOrchestrationInstanceCoordinator? _instanceCoordinator;

        public MuleRemoteCommandDispatcher(
            IMuleClient muleClient,
            IMuleStorage muleStorage,
            IMuleSerializer muleSerializer,
            IMuleCommitNotifier commitNotifier,
            IServiceProvider serviceProvider,
            IServiceScopeFactory scopeFactory,
            ILogger<MuleRemoteCommandDispatcher> logger,
            ITaskDispatchRepository? dispatchRepository = null,
            IOrchestrationDispatchCancellationRecoveryCoordinator? cancellationRecoveryCoordinator = null,
            IOrchestrationInstanceCoordinator? instanceCoordinator = null)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
            _muleStorage = muleStorage ?? throw new ArgumentNullException(nameof(muleStorage));
            _muleSerializer = muleSerializer ?? throw new ArgumentNullException(nameof(muleSerializer));
            _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dispatchRepository = dispatchRepository;
            _cancellationRecoveryCoordinator = cancellationRecoveryCoordinator;
            _instanceCoordinator = instanceCoordinator;
        }

        public async Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.ScheduledOnUtc is { } scheduledOnUtc &&
                scheduledOnUtc > DateTimeOffset.UtcNow)
            {
                await EnqueueScheduledAsync(command, scheduledOnUtc, cancellationToken);
                return;
            }

            if (HasRuntimeDispatchState(command))
            {
                try
                {
                    var directAction = ActivatorUtilities.CreateInstance<RemoteCommandDispatchAction>(_serviceProvider);
                    await directAction.ExecuteDirectAsync(command, cancellationToken);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (RemoteCommandConfigurationException exception)
                {
                    // The direct action has already persisted this deterministic terminal
                    // failure. Enqueuing the same invalid command cannot repair it.
                    _logger.LogError(
                        exception,
                        "Direct remote command dispatch '{DispatchId}' has a terminal configuration failure.",
                        command.DispatchId);
                    return;
                }
                catch (Exception exception)
                {
                    // QueuePending is already durable. Mule is materialized only for the
                    // failed/uncertain path and keeps its normal retry and recovery policy.
                    _logger.LogDebug(
                        exception,
                        "Direct remote command dispatch '{DispatchId}' failed; activating durable Mule recovery.",
                        command.DispatchId);
                }
            }

            global::Mule.Ulid actionId;
            try
            {
                actionId = await _muleClient.EnqueueAsync(
                    MuleActionKeys.RemoteCommandDispatchActionKey,
                    command,
                    options =>
                    {
                        options.Lane = MuleActionKeys.DispatchLane;
                        options.CorrelationId = command.OrchestrationInstanceId;
                        options.DeduplicationKey = BuildDeduplicationKey(command);
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RemoteCommandDeliveryUncertainException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new RemoteCommandDeliveryUncertainException(
                    $"Mule could not confirm whether it accepted remote command dispatch '{command.DispatchId}'.",
                    exception);
            }

            await CaptureActionReferenceAsync(command, actionId, MuleActionKeys.DispatchLane, cancellationToken);
        }

        private async Task EnqueueScheduledAsync(
            RemoteCommand command,
            DateTimeOffset scheduledOnUtc,
            CancellationToken cancellationToken)
        {
            var action = new DurableAction
            {
                Id = global::Mule.Ulid.NewUlid(),
                Key = MuleActionKeys.RemoteCommandDispatchActionKey,
                Lane = MuleActionKeys.DispatchLane,
                Payload = _muleSerializer.Serialize(command),
                PayloadType = typeof(RemoteCommand).AssemblyQualifiedName!,
                Metadata = null,
                CorrelationId = command.OrchestrationInstanceId,
                DeduplicationKey = BuildDeduplicationKey(command),
                Status = DurableActionStatus.Pending,
                CreatedOnUtc = DateTimeOffset.UtcNow,
                NextAttemptOnUtc = scheduledOnUtc
            };

            global::Mule.Ulid actionId;
            try
            {
                await _muleStorage.AddAsync(action, cancellationToken);
                await _muleStorage.SaveChangesAsync(cancellationToken);

                actionId = string.IsNullOrWhiteSpace(action.DeduplicationKey)
                    ? action.Id
                    : await _muleStorage.FindByDeduplicationKeyAsync(
                        action.Key,
                        action.DeduplicationKey,
                        cancellationToken) ?? action.Id;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RemoteCommandDeliveryUncertainException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new RemoteCommandDeliveryUncertainException(
                    $"Mule could not confirm whether it persisted scheduled remote command dispatch '{command.DispatchId}'.",
                    exception);
            }

            await CaptureActionReferenceAsync(command, actionId, action.Lane, cancellationToken);
            await _commitNotifier.NotifySavedAsync(actionId, action.Lane, cancellationToken);

        }

        private async Task CaptureActionReferenceAsync(
            RemoteCommand command,
            global::Mule.Ulid actionId,
            string lane,
            CancellationToken cancellationToken)
        {
            var dispatchRepository = _dispatchRepository;
            if (dispatchRepository is null ||
                string.IsNullOrWhiteSpace(command.DispatchId) ||
                !System.Ulid.TryParse(command.DispatchId, out var dispatchValue))
            {
                return;
            }

            try
            {
                async Task CaptureAsync(CancellationToken token)
                {
                    var dispatch = await dispatchRepository.TryGetById(new Id(dispatchValue), token);
                    if (dispatch is null)
                    {
                        return;
                    }

                    var acceptedStatus = command.ScheduledOnUtc is { } scheduledOnUtc &&
                        scheduledOnUtc > DateTimeOffset.UtcNow ? "Scheduled" : "Enqueued";
                    if (dispatchRepository is ITaskDispatchActionReferenceRepository atomicReferences)
                    {
                        await atomicReferences.CaptureActionReference(
                            dispatch.Id, actionId.ToString(), lane, acceptedStatus, token);
                    }
                    else
                    {
                        dispatch.Metadata["Mule.ActionId"] = JsonValue.Create(actionId.ToString());
                        dispatch.Metadata["Mule.Lane"] = JsonValue.Create(lane);
                        if (string.Equals(dispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase))
                            dispatch.DispatchStatus = acceptedStatus;
                        await dispatchRepository.Update(dispatch, token);
                    }
                    if (dispatch.PhysicalCancellationPending && _cancellationRecoveryCoordinator is not null)
                    {
                        await _cancellationRecoveryCoordinator.ReconcileAsync(dispatch.Id, token);
                    }
                }

                // This row belongs to the independent dispatch graph, not to the mutable
                // orchestration cursor. Different parallel tasks update different rows and
                // must not serialize behind the instance lease.
                await CaptureAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Publishing already succeeded. A missing diagnostic reference must not create a duplicate dispatch.
                _logger.LogWarning(
                    exception,
                    "Mule action '{ActionId}' was enqueued but could not be associated with task dispatch '{DispatchId}'.",
                    actionId,
                    command.DispatchId);
            }
        }

        internal static string BuildDeduplicationKey(RemoteCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.DispatchId))
            {
                return command.DispatchId;
            }

            if (!string.IsNullOrWhiteSpace(command.TaskExecutionAttemptId))
            {
                return command.TaskExecutionAttemptId;
            }

            if (!string.IsNullOrWhiteSpace(command.TaskExecutionId))
            {
                return string.Join(
                    ":",
                    [
                        command.OrchestrationInstanceId ?? string.Empty,
                        command.TaskExecutionId,
                        command.TaskKey ?? string.Empty,
                        command.RemoteCommandTransport.ToString(),
                        command.SettingsPayload ?? string.Empty
                    ]);
            }

            return command.OrchestrationInstanceId;
        }

        private static bool HasRuntimeDispatchState(RemoteCommand command)
            => !string.IsNullOrWhiteSpace(command.OrchestrationInstanceId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionId)
                && !string.IsNullOrWhiteSpace(command.TaskExecutionAttemptId)
                && !string.IsNullOrWhiteSpace(command.DispatchId);
    }
}
