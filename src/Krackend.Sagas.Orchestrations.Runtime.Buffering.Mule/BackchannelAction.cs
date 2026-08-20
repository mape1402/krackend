using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    [MuleAction(MuleActionKeys.BackchannelAction)]
    public class BackchannelAction : IMuleAction<WorkItem>
    {
        private readonly ISagaEngine _sagaEngine;

        public BackchannelAction(ISagaEngine sagaEngine)
        {
            _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        }

        public async ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            var workItem = context.Payload;

            var intent = new ForwardIntent
            {
                ArtifactId = workItem.ArtifactId,
                IngressTransport = workItem.IngressTransport,
                Metadata = workItem.Metadata,
                Payload = workItem.Payload
            };

            await _sagaEngine.OrchestrateAsync(intent, cancellationToken);
        }
    }
}
