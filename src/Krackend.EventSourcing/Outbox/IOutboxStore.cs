namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// Stores integration events until they are published.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Appends messages to the outbox.
    /// </summary>
    Task AppendAsync(IReadOnlyCollection<OutboxMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads unpublished messages.
    /// </summary>
    Task<IReadOnlyCollection<OutboxMessage>> ReadUnpublishedAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as published.
    /// </summary>
    Task MarkPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);
}
