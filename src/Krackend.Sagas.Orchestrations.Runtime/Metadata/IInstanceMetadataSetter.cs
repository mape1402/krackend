namespace Krackend.Sagas.Orchestrations.Runtime.Metadata
{
    public interface IInstanceMetadataSetter
    {
        public void Set(InstanceMetadata metadata);
    }
}
