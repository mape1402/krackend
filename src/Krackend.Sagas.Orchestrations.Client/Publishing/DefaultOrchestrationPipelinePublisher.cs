namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Serialization;

internal sealed class DefaultOrchestrationPipelinePublisher : IOrchestrationPipelinePublisher
{
    private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
    private readonly IOrchestrationMessageMetadataSetter _messageMetadataSetter;
    private readonly IOrchestrationPropagationMetadataAccessor _propagationMetadataAccessor;
    private readonly IOrchestrationPropagationMetadataSetter _propagationMetadataSetter;
    private readonly IOrchestrationTriggerMetadataAccessor _triggerMetadataAccessor;
    private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;
    private readonly IOrchestrationExecutionResultMetadataFactory _resultMetadataFactory;
    private readonly IOrchestrationMessageMetadataComposer _metadataComposer;
    private readonly IOrchestrationPayloadSerializer _payloadSerializer;
    private readonly IOrchestrationClientPublisher _publisher;

    public DefaultOrchestrationPipelinePublisher(
        IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
        IOrchestrationMessageMetadataSetter messageMetadataSetter,
        IOrchestrationPropagationMetadataAccessor propagationMetadataAccessor,
        IOrchestrationPropagationMetadataSetter propagationMetadataSetter,
        IOrchestrationTriggerMetadataAccessor triggerMetadataAccessor,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter,
        IOrchestrationExecutionResultMetadataFactory resultMetadataFactory,
        IOrchestrationMessageMetadataComposer metadataComposer,
        IOrchestrationPayloadSerializer payloadSerializer,
        IOrchestrationClientPublisher publisher)
    {
        _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
        _messageMetadataSetter = messageMetadataSetter ?? throw new ArgumentNullException(nameof(messageMetadataSetter));
        _propagationMetadataAccessor = propagationMetadataAccessor ?? throw new ArgumentNullException(nameof(propagationMetadataAccessor));
        _propagationMetadataSetter = propagationMetadataSetter ?? throw new ArgumentNullException(nameof(propagationMetadataSetter));
        _triggerMetadataAccessor = triggerMetadataAccessor ?? throw new ArgumentNullException(nameof(triggerMetadataAccessor));
        _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
        _resultMetadataFactory = resultMetadataFactory ?? throw new ArgumentNullException(nameof(resultMetadataFactory));
        _metadataComposer = metadataComposer ?? throw new ArgumentNullException(nameof(metadataComposer));
        _payloadSerializer = payloadSerializer ?? throw new ArgumentNullException(nameof(payloadSerializer));
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
        var businessPayload = _payloadSerializer.ToJsonNode(payload);

        if (HasReplyAddress(messageMetadata))
        {
            try
            {
                _resultMetadataSetter.Set(_resultMetadataFactory.CreateSuccess(requestType, responseType, options));
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
            await PublishEventPayloadAsync(businessPayload, options.TriggerAddress, cancellationToken);
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
            _resultMetadataSetter.Set(_resultMetadataFactory.CreateFailure(requestType, exception, options));
            await _publisher.PublishAsync(null, messageMetadata.ReplyAddress, cancellationToken);
        }
        finally
        {
            _resultMetadataSetter.Clear();
        }
    }

    public async Task PublishEventAsync(
        Type requestType,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options?.HasTriggerDestination != true)
        {
            return;
        }

        var businessPayload = _payloadSerializer.ToJsonNode(payload);
        await PublishEventPayloadAsync(businessPayload, options.TriggerAddress, cancellationToken);
    }

    private bool HasReplyAddress(OrchestrationMessageMetadata metadata)
        => metadata?.ReplyAddress is not null
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.Transport)
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.SettingsPayload);

    private async Task PublishEventPayloadAsync(
        JsonNode businessPayload,
        OrchestrationReplyAddress triggerAddress,
        CancellationToken cancellationToken)
    {
        var previousMessageMetadata = _messageMetadataAccessor.Get();
        var previousPropagationMetadata = _propagationMetadataAccessor.Get();
        var triggerMetadata = await _triggerMetadataAccessor.GetAsync(cancellationToken)
            ?? new OrchestrationTriggerMetadata();

        try
        {
            _messageMetadataSetter.Set(_metadataComposer.ComposeEventMessageMetadata(
                previousMessageMetadata,
                previousPropagationMetadata,
                triggerMetadata,
                triggerAddress));
            _propagationMetadataSetter.Set(_metadataComposer.ComposeEventPropagationMetadata(
                previousMessageMetadata,
                previousPropagationMetadata,
                triggerMetadata,
                triggerAddress));
            await _publisher.PublishAsync(businessPayload, triggerAddress, cancellationToken);
        }
        finally
        {
            _messageMetadataSetter.Set(previousMessageMetadata);
            _propagationMetadataSetter.Set(previousPropagationMetadata);
        }
    }

}
