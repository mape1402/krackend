using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    internal class IntakeBufferMule : IIntakeBuffer
    {
        private readonly IMuleClient _muleClient;

        public IntakeBufferMule(IMuleClient muleClient)
        {
            _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
        }

        public async Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            switch (workItem.IngressKind)
            {
                case Ingress.IngressKind.Trigger:
                    await _muleClient.EnqueueAsync(MuleActionKeys.TriggerActionKey, workItem, cancellationToken);
                    break;
                
                case Ingress.IngressKind.Response:
                    await _muleClient.EnqueueAsync(MuleActionKeys.BackchannelActionKey, workItem, cancellationToken);
                    break;

                default:
                    break;
            }
        }
    }
}
