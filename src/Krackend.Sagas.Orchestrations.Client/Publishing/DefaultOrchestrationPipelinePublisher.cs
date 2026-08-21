namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Serialization;

internal sealed class DefaultOrchestrationPipelinePublisher : IOrchestrationPipelinePublisher
{
    private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
    private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;
    private readonly IOrchestrationExecutionResultMetadataFactory _resultMetadataFactory;
    private readonly IOrchestrationClientPublisher _publisher;

    public DefaultOrchestrationPipelinePublisher(
        IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter,
        IOrchestrationExecutionResultMetadataFactory resultMetadataFactory,
        IOrchestrationClientPublisher publisher)
    {
        _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
        _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
        _resultMetadataFactory = resultMetadataFactory ?? throw new ArgumentNullException(nameof(resultMetadataFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task PublishSuccessAsync(
        Type requestType,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default)
    {
        var messageMetadata = _messageMetadataAccessor.Get();
        var businessPayload = OrchestrationPayloadSerializer.ToJsonNode(payload);

        if (HasReplyAddress(messageMetadata))
        {
            try
            {
                _resultMetadataSetter.Set(_resultMetadataFactory.CreateSuccess(requestType, responseType));
                await _publisher.PublishAsync(businessPayload, messageMetadata.ReplyAddress, cancellationToken);
            }
            finally
            {
                _resultMetadataSetter.Clear();
            }

            return;
        }

        if (options?.HasTriggerDestination == true)
        {
            await _publisher.PublishAsync(businessPayload, options.TriggerAddress, cancellationToken);
        }
    }

    public async Task PublishFailureAsync(
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default)
    {
        var messageMetadata = _messageMetadataAccessor.Get();
        if (!HasReplyAddress(messageMetadata))
        {
            return;
        }

        try
        {
            _resultMetadataSetter.Set(_resultMetadataFactory.CreateFailure(requestType, exception));
            await _publisher.PublishAsync(null, messageMetadata.ReplyAddress, cancellationToken);
        }
        finally
        {
            _resultMetadataSetter.Clear();
        }
    }

    private bool HasReplyAddress(OrchestrationMessageMetadata metadata)
        => metadata?.ReplyAddress is not null
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.Transport)
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.SettingsPayload);
}
