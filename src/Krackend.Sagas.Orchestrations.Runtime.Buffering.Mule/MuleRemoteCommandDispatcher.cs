using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal sealed class MuleRemoteCommandDispatcher : IRemoteCommandDispatcher
    {
        private static readonly TimeSpan MaxInProcessWakeUpDelay = TimeSpan.FromMinutes(1);

        private readonly IMuleClient _muleClient;
        private readonly IMuleStorage _muleStorage;
        private readonly IMuleSerializer _muleSerializer;
        private readonly IMuleCommitNotifier _commitNotifier;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MuleRemoteCommandDispatcher> _logger;
        private readonly ITaskDispatchRepository? _dispatchRepository;

        public MuleRemoteCommandDispatcher(
            IMuleClient muleClient,
            IMuleStorage muleStorage,
            IMuleSerializer muleSerializer,
            IMuleCommitNotifier commitNotifier,
            IServiceScopeFactory scopeFactory,
            ILogger<MuleRemoteCommandDispatcher> logger,
            ITaskDispatchRepository? dispatchRepository = null)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
            _muleStorage = muleStorage ?? throw new ArgumentNullException(nameof(muleStorage));
            _muleSerializer = muleSerializer ?? throw new ArgumentNullException(nameof(muleSerializer));
            _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dispatchRepository = dispatchRepository;
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

            global::Mule.Ulid actionId;
            try
            {
                actionId = await _muleClient.EnqueueAsync(
                    MuleActionKeys.RemoteCommandDispatchActionKey,
                    command,
                    options =>
                    {
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

            await CaptureActionReferenceAsync(command, actionId, MuleSettings.DefaultLane, cancellationToken);
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
                Lane = MuleSettings.DefaultLane,
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
            ScheduleDueNotification(actionId, action.Lane, scheduledOnUtc);
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
                var dispatch = await dispatchRepository.TryGetById(new Id(dispatchValue), cancellationToken);
                if (dispatch is null)
                {
                    return;
                }

                dispatch.Metadata["Mule.ActionId"] = JsonValue.Create(actionId.ToString());
                dispatch.Metadata["Mule.Lane"] = JsonValue.Create(lane);
                await dispatchRepository.Update(dispatch, cancellationToken);
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

        private void ScheduleDueNotification(global::Mule.Ulid actionId, string lane, DateTimeOffset scheduledOnUtc)
        {
            var delay = scheduledOnUtc - DateTimeOffset.UtcNow;
            if (delay > MaxInProcessWakeUpDelay)
            {
                return;
            }

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _ = Task.Run(async () =>
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var commitNotifier = scope.ServiceProvider.GetRequiredService<IMuleCommitNotifier>();
                    await commitNotifier.NotifySavedAsync(actionId, lane, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Mule scheduled remote command dispatch action '{ActionId}' could not be re-notified when it became due.",
                        actionId);
                }
            });
        }

        private static string BuildDeduplicationKey(RemoteCommand command)
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
    }
}
