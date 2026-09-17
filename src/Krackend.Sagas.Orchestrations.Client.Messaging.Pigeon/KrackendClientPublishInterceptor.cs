namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Producing;

internal sealed class KrackendClientPublishInterceptor : IPublishInterceptor
{
    private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
    private readonly IOrchestrationExecutionResultMetadataAccessor _resultMetadataAccessor;

    public KrackendClientPublishInterceptor(
        IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
        IOrchestrationExecutionResultMetadataAccessor resultMetadataAccessor)
    {
        _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
        _resultMetadataAccessor = resultMetadataAccessor ?? throw new ArgumentNullException(nameof(resultMetadataAccessor));
    }

    public ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default)
    {
        var messageMetadata = _messageMetadataAccessor.Get();
        if (HasMessageMetadata(messageMetadata))
        {
            publishContext.AddMetadata(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey, messageMetadata);
        }

        var resultMetadata = _resultMetadataAccessor.Get();
        if (resultMetadata is not null)
        {
            publishContext.AddMetadata(OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey, resultMetadata);
        }

        return ValueTask.CompletedTask;
    }

    private bool HasMessageMetadata(OrchestrationMessageMetadata metadata)
        => !string.IsNullOrWhiteSpace(metadata?.SagaId)
            || !string.IsNullOrWhiteSpace(metadata?.OrchestrationInstanceId)
            || !string.IsNullOrWhiteSpace(metadata?.CorrelationId)
            || !string.IsNullOrWhiteSpace(metadata?.TaskExecutionId)
            || metadata?.ReplyAddress is not null;
}
