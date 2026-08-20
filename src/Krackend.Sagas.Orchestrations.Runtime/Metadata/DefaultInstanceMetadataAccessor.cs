namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    internal sealed class DefaultInstanceMetadataAccessor : IInstanceMetadataAccessor, IInstanceMetadataSetter
    {
        private InstanceMetadata _metadata = new();

        public InstanceMetadata Get()
            => _metadata;

        public void Set(InstanceMetadata metadata)
            => _metadata = metadata ?? new InstanceMetadata();
    }
}
