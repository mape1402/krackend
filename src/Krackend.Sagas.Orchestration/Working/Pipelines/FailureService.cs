
namespace Krackend.Sagas.Orchestration.Working.Pipelines
{
    using Krackend.Sagas.Orchestration.Core;
    using Krackend.Sagas.Orchestration.Working;
    using Pigeon.Messaging.Producing;
    using Spider.Pipelines.Core;

    /// <summary>
    /// Provides the implementation for forwarding operation errors within an orchestration pipeline, including error metadata extraction and topic selection.
    /// </summary>
    internal sealed class FailureService : IFailureService
    {
        private readonly IOrchestrationContext _orchestrationContext;
        private readonly IMetadataSetter _metadataSetter;
        private readonly IExceptionEvaluator _exceptionEvaluator;
        private readonly IProducer _producer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FailureService"/> class.
        /// </summary>
        /// <param name="orchestrationContext">The orchestration context providing metadata for forwarding.</param>
        /// <param name="metadataSetter">The metadata setter used to mark the operation as failure.</param>
        /// <param name="exceptionEvaluator">The evaluator used to extract error metadata from exceptions.</param>
        /// <param name="producer">The producer used to publish error messages.</param>
        public FailureService(IOrchestrationContext orchestrationContext, IMetadataSetter metadataSetter, IExceptionEvaluator exceptionEvaluator, IProducer producer)
        {
            _orchestrationContext = orchestrationContext ?? throw new ArgumentNullException(nameof(orchestrationContext));
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
            _exceptionEvaluator = exceptionEvaluator ?? throw new ArgumentNullException(nameof(exceptionEvaluator));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        /// <inheritdoc />
        public async ValueTask ForwardError<TRequest>(IReadOnlyContext<TRequest> context, string topic, Func<TRequest, object> transformPayload)
        {
            if (!_orchestrationContext.HasMetadata())
                return;

            var errorMetadata = _exceptionEvaluator.ExtractMetadata(context.Exception);
            _metadataSetter.SetAsFailure(errorMetadata);
            var metadata = _orchestrationContext.GetMetadata();

            object payload = transformPayload != null ? transformPayload(context.Request) : context.Request;

            var replyTo = string.IsNullOrEmpty(topic) ? metadata.ReplyTo : topic;

            await _producer.PublishAsync(payload, replyTo, context.CancellationToken);
        }
    }
}
