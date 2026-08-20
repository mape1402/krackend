namespace Krackend.Sagas.Orchestrations.Runtime.Buffering
{
    public interface IIntakeBuffer
    {
        public Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    }
}
