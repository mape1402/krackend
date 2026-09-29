namespace Krackend.Sagas.Orchestrations.Tests.Client.Support;

using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class RecordingOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    private readonly IOrchestrationExecutionResultMetadataAccessor _metadataAccessor;
    private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
    private readonly IOrchestrationPropagationMetadataAccessor _propagationMetadataAccessor;
    private readonly ConcurrentQueue<PublishedOrchestrationMessage> _messages = new();
    private int _publishCount;

    public RecordingOrchestrationClientPublisher(
        IOrchestrationExecutionResultMetadataAccessor metadataAccessor,
        IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
        IOrchestrationPropagationMetadataAccessor propagationMetadataAccessor)
    {
        _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
        _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
        _propagationMetadataAccessor = propagationMetadataAccessor ?? throw new ArgumentNullException(nameof(propagationMetadataAccessor));
    }

    public object? Payload { get; private set; }

    public OrchestrationReplyAddress? Address { get; private set; }

    public OrchestrationExecutionResultMetadata? ResultMetadata { get; private set; }

    public OrchestrationMessageMetadata? MessageMetadata { get; private set; }

    public OrchestrationPropagationMetadata? PropagationMetadata { get; private set; }

    public int PublishCount => Volatile.Read(ref _publishCount);

    public IReadOnlyCollection<PublishedOrchestrationMessage> Messages => _messages.ToArray();

    public Task PublishAsync(
        object? payload,
        OrchestrationReplyAddress address,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _publishCount);
        var resultMetadata = _metadataAccessor.Get();
        var messageMetadata = _messageMetadataAccessor.Get();
        var propagationMetadata = _propagationMetadataAccessor.Get();

        Payload = payload;
        Address = address;
        ResultMetadata = resultMetadata;
        MessageMetadata = messageMetadata;
        PropagationMetadata = propagationMetadata;
        _messages.Enqueue(new PublishedOrchestrationMessage(payload, address, resultMetadata, messageMetadata, propagationMetadata));
        return Task.CompletedTask;
    }
}

internal sealed record PublishedOrchestrationMessage(
    object? Payload,
    OrchestrationReplyAddress? Address,
    OrchestrationExecutionResultMetadata? ResultMetadata,
    OrchestrationMessageMetadata? MessageMetadata,
    OrchestrationPropagationMetadata? PropagationMetadata);
