namespace Krackend.Sagas.Orchestrations.Runtime.Buffering
{
    /// <summary>
    /// Accepts ingress work and delegates it to the runtime execution buffer.
    /// </summary>
    public interface IIntakeBuffer
    {
        /// <summary>
        /// Enqueues a work item accepted from runtime ingress.
        /// </summary>
        /// <param name="workItem">Ingress work item.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous enqueue operation.</returns>
        public Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default);
    }
}
