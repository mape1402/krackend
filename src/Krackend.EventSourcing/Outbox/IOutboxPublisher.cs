namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// Publishes outbox messages to an external broker or event log.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Publishes a message.
    /// </summary>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
