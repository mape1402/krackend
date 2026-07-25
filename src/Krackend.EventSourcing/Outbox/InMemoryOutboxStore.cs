namespace Krackend.EventSourcing.Outbox;

/// <summary>
/// In-memory outbox store for tests and local development.
/// </summary>
public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly object _syncRoot = new();
    private readonly List<OutboxMessage> _messages = [];

    /// <inheritdoc />
    public Task AppendAsync(IReadOnlyCollection<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _messages.AddRange(messages);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<OutboxMessage>> ReadUnpublishedAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var messages = _messages
                .Where(message => message.PublishedAt is null)
                .OrderBy(message => message.OccurredAt)
                .Take(maxCount)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<OutboxMessage>>(messages);
        }
    }

    /// <inheritdoc />
    public Task MarkPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var index = _messages.FindIndex(message => message.MessageId == messageId);

            if (index >= 0)
                _messages[index] = _messages[index] with { PublishedAt = publishedAt };

            return Task.CompletedTask;
        }
    }
}
