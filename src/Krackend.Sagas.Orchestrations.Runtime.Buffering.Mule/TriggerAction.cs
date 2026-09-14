using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    /// <summary>
    /// Processes durable trigger work items and starts orchestration instances.
    /// </summary>
    [MuleAction(MuleActionKeys.TriggerAction)]
    public class TriggerAction : IMuleAction<WorkItem>
    {
        private readonly ISagaEngine _sagaEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerAction"/> class.
        /// </summary>
        /// <param name="sagaEngine">Saga engine that starts orchestration instances.</param>
        public TriggerAction(ISagaEngine sagaEngine)
        {
            _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        }

        /// <inheritdoc />
        public async ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            var workItem = context.Payload;

            var intent = new StartIntent
            {
                ArtifactId = workItem.ArtifactId,
                MessageMetadata = workItem.MessageMetadata,
                IngressTransport = workItem.IngressTransport,
                Payload = workItem.Payload
            };

            await _sagaEngine.StartOrchestrationAsync(intent, cancellationToken);
        }
    }
}
