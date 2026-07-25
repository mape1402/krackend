namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// Publishes pending outbox messages and marks successful publications.
/// </summary>
public sealed class OutboxPublishingService
{
    private readonly IOutboxStore _outboxStore;
    private readonly IOutboxPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxPublishingService"/> class.
    /// </summary>
    public OutboxPublishingService(IOutboxStore outboxStore, IOutboxPublisher publisher)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Publishes a batch of pending outbox messages.
    /// </summary>
    public async Task<int> PublishBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var messages = await _outboxStore.ReadUnpublishedAsync(maxCount, cancellationToken);
        var published = 0;

        foreach (var message in messages)
        {
            await _publisher.PublishAsync(message, cancellationToken);
            await _outboxStore.MarkPublishedAsync(message.MessageId, DateTimeOffset.UtcNow, cancellationToken);
            published++;
        }

        return published;
    }
}
