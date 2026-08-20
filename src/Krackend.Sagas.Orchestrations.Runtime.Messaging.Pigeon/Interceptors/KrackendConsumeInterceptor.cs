using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Pigeon.Messaging.Consuming.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors
{
    internal class KrackendConsumeInterceptor : IConsumeInterceptor
    {
        private readonly IInstanceMetadataSetter _metadataSetter;

        public KrackendConsumeInterceptor(IInstanceMetadataSetter metadataSetter)
        {
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
        }

        public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var metadata = context.GetMetadata<InstanceMetadata>(MetadataConstants.InstanceMetadataKey);
                _metadataSetter.Set(metadata);
            }
            catch
            {
                _metadataSetter.Set(new InstanceMetadata());
            }

            return ValueTask.CompletedTask;
        }
    }
}
