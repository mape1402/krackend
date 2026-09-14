namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    /// <summary>
    /// Publishes orchestration commands through a messaging transport provider.
    /// </summary>
    public interface IMessagingDispatchAdapter
    {
        /// <summary>
        /// Publishes the supplied messaging command.
        /// </summary>
        /// <param name="command">Command to publish.</param>
        /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
        /// <returns>A task that completes when the command has been published.</returns>
        Task PublishAsync(MessagingCommand command, CancellationToken cancellationToken = default);
    }
}
