using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors
{
    internal class KrackendPublishInterceptor : IPublishInterceptor
    {
        private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
        private readonly IOrchestrationExecutionResultMetadataAccessor _resultMetadataAccessor;
        private readonly IOrchestrationPropagationMetadataAccessor _propagationMetadataAccessor;
        private readonly PigeonPropagationMetadataMapper _propagationMetadataMapper = new();

        public KrackendPublishInterceptor(
            IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
            IOrchestrationExecutionResultMetadataAccessor resultMetadataAccessor)
            : this(messageMetadataAccessor, resultMetadataAccessor, null)
        {
        }

        public KrackendPublishInterceptor(
            IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
            IOrchestrationExecutionResultMetadataAccessor resultMetadataAccessor,
            IOrchestrationPropagationMetadataAccessor propagationMetadataAccessor)
        {
            _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
            _resultMetadataAccessor = resultMetadataAccessor ?? throw new ArgumentNullException(nameof(resultMetadataAccessor));
            _propagationMetadataAccessor = propagationMetadataAccessor;
        }

        public ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default)
        {
            var messageMetadata = _messageMetadataAccessor.Get();
            if (HasMessageMetadata(messageMetadata))
            {
                publishContext.AddMetadata(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey, messageMetadata);
            }

            var resultMetadata = _resultMetadataAccessor.Get();
            if (resultMetadata is not null)
            {
                publishContext.AddMetadata(OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey, resultMetadata);
            }

            var propagationMetadata = _propagationMetadataAccessor?.Get();
            _propagationMetadataMapper.Attach(publishContext, propagationMetadata);

            return ValueTask.CompletedTask;
        }

        private bool HasMessageMetadata(OrchestrationMessageMetadata metadata)
            => !string.IsNullOrWhiteSpace(metadata?.SagaId)
                || !string.IsNullOrWhiteSpace(metadata?.OrchestrationInstanceId)
                || !string.IsNullOrWhiteSpace(metadata?.CorrelationId)
                || !string.IsNullOrWhiteSpace(metadata?.TaskExecutionId)
                || metadata?.ReplyAddress is not null;
    }
}
