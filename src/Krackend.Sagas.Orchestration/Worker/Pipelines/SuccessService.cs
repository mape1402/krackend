namespace Krackend.Sagas.Orchestration.Worker.Pipelines
{
    using Krackend.Sagas.Orchestration.Core;
    using Pigeon.Messaging.Producing;
    using Spider.Pipelines.Core;

    /// <summary>
    /// Provides the implementation for forwarding operations within an orchestration pipeline, including payload transformation and topic selection.
    /// </summary>
    internal sealed class SuccessService : ISuccessService
    {
        private readonly IOrchestrationContext _orchestrationContext;
        private readonly IMetadataSetter _metadataSetter;
        private readonly IProducer _producer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SuccessService"/> class.
        /// </summary>
        /// <param name="orchestrationContext">The orchestration context providing metadata for forwarding.</param>
        /// <param name="metadataSetter">The metadata setter used to mark the operation as success.</param>
        /// <param name="producer">The producer used to publish messages.</param>
        public SuccessService(IOrchestrationContext orchestrationContext, IMetadataSetter metadataSetter, IProducer producer)
        {
            _orchestrationContext = orchestrationContext ?? throw new ArgumentNullException(nameof(orchestrationContext));
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        /// <inheritdoc />
        public async ValueTask Forward<TRequest>(IReadOnlyContext<TRequest> context, string topic, Func<TRequest, object> transformPayload)
        {
            if (!_orchestrationContext.HasMetadata())
                return;

            _metadataSetter.SetAsSuccess();
            var metadata = _orchestrationContext.GetMetadata();

            object payload = transformPayload != null ? transformPayload(context.Request) : context.Request;

            var replyTo = string.IsNullOrEmpty(topic) ? metadata.OrchestrationTopic : topic;

            await _producer.PublishAsync(payload, replyTo, context.CancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask Forward<TRequest, TResponse>(IReadOnlyContext<TRequest, TResponse> context, string topic, Func<TRequest, TResponse, object> transformPayload)
        {
            if (!_orchestrationContext.HasMetadata())
                return;

            _metadataSetter.SetAsSuccess();
            var metadata = _orchestrationContext.GetMetadata();

            object payload = transformPayload != null ? transformPayload(context.Request, context.Response) : context.Response;

            var replyTo = string.IsNullOrEmpty(topic) ? metadata.OrchestrationTopic : topic;

            await _producer.PublishAsync(payload, replyTo, context.CancellationToken);
        }
    }
}
