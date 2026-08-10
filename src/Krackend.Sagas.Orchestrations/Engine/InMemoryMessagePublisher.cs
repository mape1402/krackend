using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// In-memory publisher used as the default Beta 1 fallback before a broker adapter is enabled.
/// </summary>
public sealed class InMemoryMessagePublisher : IMessagePublisher
{
    private readonly ConcurrentQueue<MessagePublishRequest> _messages = new();

    /// <inheritdoc/>
    public Task<MessagePublishResult> Publish(MessagePublishRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        _messages.Enqueue(request);
        return Task.FromResult(new MessagePublishResult
        {
            Succeeded = true,
            Status = "Dispatched",
            ExternalReference = request.Topic
        });
    }
}
