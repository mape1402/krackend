using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;

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

        public MuleRemoteCommandDispatcher(
            IMuleClient muleClient,
            IMuleStorage muleStorage,
            IMuleSerializer muleSerializer,
            IMuleCommitNotifier commitNotifier,
            IServiceScopeFactory scopeFactory,
            ILogger<MuleRemoteCommandDispatcher> logger)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
            _muleStorage = muleStorage ?? throw new ArgumentNullException(nameof(muleStorage));
            _muleSerializer = muleSerializer ?? throw new ArgumentNullException(nameof(muleSerializer));
            _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            await _muleClient.EnqueueAsync(
                MuleActionKeys.RemoteCommandDispatchActionKey,
                command,
                options =>
                {
                    options.CorrelationId = command.OrchestrationInstanceId;
                    options.DeduplicationKey = BuildDeduplicationKey(command);
                },
                cancellationToken);
        }

        private async Task EnqueueScheduledAsync(
            RemoteCommand command,
            DateTimeOffset scheduledOnUtc,
            CancellationToken cancellationToken)
        {
            var action = new DurableAction
            {
                Id = Guid.NewGuid(),
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

            await _muleStorage.AddAsync(action, cancellationToken);
            await _muleStorage.SaveChangesAsync(cancellationToken);

            var actionId = string.IsNullOrWhiteSpace(action.DeduplicationKey)
                ? action.Id
                : await _muleStorage.FindByDeduplicationKeyAsync(
                    action.Key,
                    action.DeduplicationKey,
                    cancellationToken) ?? action.Id;

            await _commitNotifier.NotifySavedAsync(actionId, action.Lane, cancellationToken);
            ScheduleDueNotification(actionId, action.Lane, scheduledOnUtc);
        }

        private void ScheduleDueNotification(Guid actionId, string lane, DateTimeOffset scheduledOnUtc)
        {
            var delay = scheduledOnUtc - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero || delay > MaxInProcessWakeUpDelay)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(delay);

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
