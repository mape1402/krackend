using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    [MuleAction(MuleActionKeys.TriggerAction)]
    public class TriggerAction : IMuleAction<WorkItem>
    {
        private readonly ISagaEngine _sagaEngine;

        public TriggerAction(ISagaEngine sagaEngine)
        {
            _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        }

        public async ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            var workItem = context.Payload;

            var intent = new StartIntent
            {
                ArtifactId = workItem.ArtifactId,
                Metadata = workItem.Metadata,
                IngressTransport = workItem.IngressTransport,
                Payload = workItem.Payload
            };

            await _sagaEngine.StartOrchestrationAsync(intent, cancellationToken);
        }
    }
}
