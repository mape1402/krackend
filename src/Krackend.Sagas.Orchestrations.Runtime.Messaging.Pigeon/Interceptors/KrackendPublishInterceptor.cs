using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors
{
    internal class KrackendPublishInterceptor : IPublishInterceptor
    {
        private readonly IInstanceMetadataAccessor _metadataAccessor;

        public KrackendPublishInterceptor(IInstanceMetadataAccessor metadataAccessor)
        {
            _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
        }

        public ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default)
        {
            var metadata = _metadataAccessor.Get();
            publishContext.AddMetadata(MetadataConstants.InstanceMetadataKey, metadata);

            return ValueTask.CompletedTask;
        }
    }
}
