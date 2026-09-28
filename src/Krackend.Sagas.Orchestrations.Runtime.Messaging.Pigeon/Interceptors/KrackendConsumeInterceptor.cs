using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Consuming.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors
{
    internal class KrackendConsumeInterceptor : IConsumeInterceptor
    {
        private readonly IOrchestrationMessageMetadataSetter _metadataSetter;
        private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;
        private readonly IOrchestrationPropagationMetadataSetter _propagationMetadataSetter;
        private readonly PigeonPropagationMetadataMapper _propagationMetadataMapper = new();

        public KrackendConsumeInterceptor(
            IOrchestrationMessageMetadataSetter metadataSetter,
            IOrchestrationExecutionResultMetadataSetter resultMetadataSetter)
            : this(metadataSetter, resultMetadataSetter, null)
        {
        }

        public KrackendConsumeInterceptor(
            IOrchestrationMessageMetadataSetter metadataSetter,
            IOrchestrationExecutionResultMetadataSetter resultMetadataSetter,
            IOrchestrationPropagationMetadataSetter propagationMetadataSetter)
        {
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
            _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
            _propagationMetadataSetter = propagationMetadataSetter;
        }

        public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
        {
            _resultMetadataSetter.Clear();

            try
            {
                var metadata = context.GetMetadata<OrchestrationMessageMetadata>(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey);
                _metadataSetter.Set(metadata);
            }
            catch
            {
                _metadataSetter.Set(new OrchestrationMessageMetadata());
            }

            try
            {
                var resultMetadata = context.GetMetadata<OrchestrationExecutionResultMetadata>(OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey);
                _resultMetadataSetter.Set(resultMetadata);
            }
            catch
            {
                _resultMetadataSetter.Clear();
            }

            _propagationMetadataSetter?.Set(_propagationMetadataMapper.Capture(context));

            return ValueTask.CompletedTask;
        }
    }
}
