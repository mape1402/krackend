using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal class IntakeBufferMule : IIntakeBuffer
    {
        private readonly IMuleClient _muleClient;
        private readonly ILogger<IntakeBufferMule> _logger;

        public IntakeBufferMule(IMuleClient muleClient, ILogger<IntakeBufferMule> logger)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
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
                    await _muleClient.EnqueueAsync(
                        MuleActionKeys.TriggerActionKey,
                        workItem,
                        options =>
                        {
                            options.CorrelationId = workItem.MessageMetadata?.CorrelationId
                                ?? workItem.MessageMetadata?.SagaId;
                        },
                        cancellationToken);
                    break;
                
                case Ingress.IngressKind.Backchannel:
                    await _muleClient.EnqueueAsync(
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
