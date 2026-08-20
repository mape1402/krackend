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
                    await _muleClient.EnqueueAsync(MuleActionKeys.TriggerActionKey, workItem, cancellationToken);
                    break;
                
                case Ingress.IngressKind.Backchannel:
                    await _muleClient.EnqueueAsync(MuleActionKeys.BackchannelActionKey, workItem, cancellationToken);
                    break;

                default:
                    _logger.LogWarning(
                        "Runtime work item for artifact '{artifactId}' was ignored because ingress kind '{ingressKind}' is not supported.",
                        workItem.ArtifactId,
                        workItem.IngressKind);
                    break;
            }
        }
    }
}
