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
            await PublishTriggerAsync(businessPayload, options.TriggerAddress, cancellationToken);
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

    private bool HasReplyAddress(OrchestrationMessageMetadata metadata)
        => metadata?.ReplyAddress is not null
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.Transport)
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.SettingsPayload);

    private async Task PublishTriggerAsync(
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
            _messageMetadataSetter.Set(CreateTriggerMessageMetadata(previousMessageMetadata, triggerMetadata));
            _propagationMetadataSetter.Set(CreateTriggerPropagationMetadata(previousPropagationMetadata, triggerMetadata));
            await _publisher.PublishAsync(businessPayload, triggerAddress, cancellationToken);
        }
        finally
        {
            _messageMetadataSetter.Set(previousMessageMetadata);
            _propagationMetadataSetter.Set(previousPropagationMetadata);
        }
    }

    private static OrchestrationMessageMetadata CreateTriggerMessageMetadata(
        OrchestrationMessageMetadata previous,
        OrchestrationTriggerMetadata triggerMetadata)
        => new()
        {
            SagaId = previous?.SagaId,
            OrchestrationInstanceId = previous?.OrchestrationInstanceId,
            CurrentStage = previous?.CurrentStage,
            CurrentTasks = previous?.CurrentTasks,
            CorrelationId = FirstNonEmpty(triggerMetadata?.CorrelationId, previous?.CorrelationId),
            TaskExecutionId = previous?.TaskExecutionId,
            DispatchId = previous?.DispatchId,
            Attempt = previous?.Attempt ?? 0,
            ReplyAddress = previous?.ReplyAddress
        };

    private static OrchestrationPropagationMetadata CreateTriggerPropagationMetadata(
        OrchestrationPropagationMetadata previous,
        OrchestrationTriggerMetadata triggerMetadata)
    {
        var metadata = previous?.Clone() ?? new OrchestrationPropagationMetadata();
        metadata.Items[OrchestrationMetadataConstants.TriggerMetadataKey] = CreateTriggerMetadataPayload(triggerMetadata);
        return metadata;
    }

    private static JsonNode CreateTriggerMetadataPayload(OrchestrationTriggerMetadata metadata)
        => metadata?.ToJson() ?? new JsonObject();

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
