using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
{
    /// <summary>
    /// Processes durable backchannel work items and forwards them to the saga engine.
    /// </summary>
    [MuleAction(MuleActionKeys.BackchannelAction)]
    public class BackchannelAction : IMuleAction<WorkItem>
    {
        private readonly ISagaEngine _sagaEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackchannelAction"/> class.
        /// </summary>
        /// <param name="sagaEngine">Saga engine that advances orchestration instances.</param>
        public BackchannelAction(ISagaEngine sagaEngine)
        {
            _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        }

        /// <inheritdoc />
        public async ValueTask ExecuteAsync(MuleActionContext<WorkItem> context, CancellationToken cancellationToken)
        {
            var workItem = context.Payload;

            var intent = new ForwardIntent
            {
                ArtifactId = workItem.ArtifactId,
                IngressTransport = workItem.IngressTransport,
                MessageMetadata = workItem.MessageMetadata,
                ExecutionResultMetadata = workItem.ExecutionResultMetadata,
                Payload = workItem.Payload
            };

            await _sagaEngine.OrchestrateAsync(intent, cancellationToken);
        }
    }
}
