namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

/// <summary>
/// Publishes runtime messages through a broker-neutral facade.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the configured topic and version.
    /// </summary>
    /// <param name="request">Publish request containing destination, version and payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Technical publication result.</returns>
    Task<MessagePublishResult> Publish(
        MessagePublishRequest request,
        CancellationToken cancellationToken = default);
}
