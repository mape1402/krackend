using Microsoft.Extensions.Logging;
using Mule;
using Mule.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal class IntakeBufferMule : IIntakeBuffer
    {
        private readonly IMuleStorage _muleStorage;
        private readonly IMuleSerializer _muleSerializer;
        private readonly IMuleCommitNotifier _commitNotifier;
        private readonly ILogger<IntakeBufferMule> _logger;

        public IntakeBufferMule(
            IMuleStorage muleStorage,
            IMuleSerializer muleSerializer,
            IMuleCommitNotifier commitNotifier,
            ILogger<IntakeBufferMule> logger)
        {
            _muleStorage = muleStorage ?? throw new ArgumentNullException(nameof(muleStorage));
            _muleSerializer = muleSerializer ?? throw new ArgumentNullException(nameof(muleSerializer));
            _commitNotifier = commitNotifier ?? throw new ArgumentNullException(nameof(commitNotifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            if (workItem is null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            switch (workItem.IngressKind)
            {
                case Ingress.IngressKind.Trigger:
                    await EnqueueDurableAsync(
                        MuleActionKeys.TriggerActionKey,
                        workItem,
                        options =>
                        {
                            options.CorrelationId = workItem.MessageMetadata?.CorrelationId
                                ?? workItem.MessageMetadata?.SagaId;
                            options.DeduplicationKey = BuildTriggerDeduplicationKey(workItem);
                        },
                        cancellationToken);
                    break;

                case Ingress.IngressKind.Backchannel:
                    await EnqueueDurableAsync(
                        MuleActionKeys.BackchannelActionKey,
                        workItem,
                        options =>
                        {
                            options.CorrelationId = workItem.MessageMetadata?.OrchestrationInstanceId
                                ?? workItem.MessageMetadata?.CorrelationId;
                            options.DeduplicationKey = BuildBackchannelDeduplicationKey(workItem);
                        },
                        cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "Runtime work item for artifact '{artifactId}' was ignored because ingress kind '{ingressKind}' is not supported.",
                        workItem.ArtifactId,
                        workItem.IngressKind);
                    break;
            }
        }

        private async Task EnqueueDurableAsync(
            ActionKey actionKey,
            WorkItem workItem,
            Action<EnqueueOptions> configureOptions,
            CancellationToken cancellationToken)
        {
            var options = new EnqueueOptions();
            configureOptions(options);

            if (!string.IsNullOrWhiteSpace(options.DeduplicationKey))
            {
                var existingActionId = await _muleStorage.FindByDeduplicationKeyAsync(
                    actionKey,
                    options.DeduplicationKey,
                    cancellationToken);
                if (existingActionId is not null)
                {
                    await NotifySavedActionAsync(existingActionId.Value, MuleSettings.DefaultLane, actionKey, cancellationToken);
                    return;
                }
            }

            var action = new DurableAction
            {
                Id = Guid.NewGuid(),
                Key = actionKey,
                Lane = MuleSettings.DefaultLane,
                Payload = _muleSerializer.Serialize(workItem),
                PayloadType = typeof(WorkItem).AssemblyQualifiedName!,
                Metadata = null,
                CorrelationId = options.CorrelationId,
                DeduplicationKey = options.DeduplicationKey,
                Status = DurableActionStatus.Pending,
                CreatedOnUtc = DateTimeOffset.UtcNow,
                NextAttemptOnUtc = DateTimeOffset.UtcNow
            };

            await _muleStorage.AddAsync(action, cancellationToken);
            await _muleStorage.SaveChangesAsync(cancellationToken);
            await NotifySavedActionAsync(action.Id, action.Lane, action.Key, cancellationToken);
        }

        private async Task NotifySavedActionAsync(
            Guid actionId,
            string lane,
            ActionKey actionKey,
            CancellationToken cancellationToken)
        {
            try
            {
                await _commitNotifier.NotifySavedAsync(
                    actionId,
                    lane,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Mule durable action '{ActionKey}' was persisted but could not notify fast-lane dispatch. The ingress transport will be allowed to redeliver the message.",
                    actionKey);
                throw;
            }
        }

        private string? BuildTriggerDeduplicationKey(WorkItem workItem)
        {
            var correlationId = workItem.MessageMetadata?.CorrelationId
                ?? workItem.MessageMetadata?.SagaId;

            return string.IsNullOrWhiteSpace(correlationId)
                ? null
                : $"trigger:{workItem.ArtifactId}:{correlationId}";
        }

        private string? BuildBackchannelDeduplicationKey(WorkItem workItem)
        {
            var metadata = workItem.MessageMetadata;
            if (metadata is null)
            {
                return null;
            }

            var dispatchId = metadata.DispatchId;
            if (!string.IsNullOrWhiteSpace(dispatchId))
            {
                return $"backchannel:{dispatchId}:{metadata.Attempt}";
            }

            var taskExecutionId = metadata.TaskExecutionId;
            if (!string.IsNullOrWhiteSpace(taskExecutionId))
            {
                return $"backchannel:{taskExecutionId}:{metadata.Attempt}";
            }

            return null;
        }
    }
}
