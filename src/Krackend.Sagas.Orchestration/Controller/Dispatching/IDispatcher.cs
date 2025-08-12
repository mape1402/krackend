namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Defines methods for dispatching messages and advancing saga execution.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Starts the saga execution on the specified topic and version with the given payload.
        /// </summary>
        /// <param name="topic">The topic to start on.</param>
        /// <param name="version">The semantic version of the topic.</param>
        /// <param name="payload">The data to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAsync(string topic, SemanticVersion version, object payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Advances the saga execution by sending the payload to the next step.
        /// </summary>
        /// <param name="payload">The data to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ForwardAsync(object payload, CancellationToken cancellationToken = default);
    }
}
