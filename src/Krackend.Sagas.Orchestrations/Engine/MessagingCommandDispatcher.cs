using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Builds orchestrator metadata for a messaging task and delegates publication to the broker-neutral publisher.
/// </summary>
public sealed class MessagingCommandDispatcher : IMessagingCommandDispatcher
{
    private readonly IMessagePublisher _publisher;
    private readonly IOrchestratorMetadataWriter _metadataWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingCommandDispatcher"/> class.
    /// </summary>
    /// <param name="publisher">Broker-neutral message publisher.</param>
    /// <param name="metadataWriter">Metadata writer used to expose dispatch metadata to publishing adapters.</param>
    public MessagingCommandDispatcher(IMessagePublisher publisher, IOrchestratorMetadataWriter metadataWriter)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _metadataWriter = metadataWriter ?? throw new ArgumentNullException(nameof(metadataWriter));
    }

    /// <inheritdoc/>
    public async Task<MessagingDispatchResult> Dispatch(MessagingDispatchCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        _metadataWriter.Set(CreateMetadata(command));
        try
        {
            var result = await _publisher.Publish(CreatePublishRequest(command), cancellationToken);
            return CreateDispatchResult(result);
        }
        finally
        {
            _metadataWriter.Clear();
        }
    }

    private static MessagePublishRequest CreatePublishRequest(MessagingDispatchCommand command)
    {
        return new MessagePublishRequest
        {
            Topic = command.Destination,
            Version = command.MessageVersion,
            Message = command.Payload
        };
    }

    private static MessagingDispatchResult CreateDispatchResult(MessagePublishResult result)
    {
        return new MessagingDispatchResult
        {
            Succeeded = result.Succeeded,
            Status = result.Status,
            ExternalReference = result.ExternalReference,
            FailureReason = result.FailureReason
        };
    }

    private static OrchestratorMessageMetadata CreateMetadata(MessagingDispatchCommand command)
    {
        return new OrchestratorMessageMetadata
        {
            SagaId = command.CorrelationId,
            OrchestrationId = command.OrchestrationDefinitionKey,
            OrchestrationKey = command.OrchestrationDefinitionKey,
            OrchestrationVersion = command.OrchestrationVersion,
            OrchestrationInstanceId = command.OrchestrationInstanceId,
            TaskExecutionId = command.TaskExecutionId,
            DispatchId = command.DispatchId,
            CorrelationId = command.CorrelationId,
            ResponseTopic = RuntimeBackChannelTopic.Build(command.OrchestrationDefinitionKey),
            ResponseVersion = command.OrchestrationVersion,
            Environment = command.EnvironmentKey,
            CurrentState = CreateRuntimeState(command)
        };
    }

    private static OrchestrationRuntimeState CreateRuntimeState(MessagingDispatchCommand command)
    {
        return new OrchestrationRuntimeState
        {
            Status = command.CurrentStatus,
            CurrentStageKey = command.StageKey,
            CurrentTaskKey = command.TaskKey,
            Attempt = command.Attempt,
            StartedOnUtc = command.StartedOnUtc,
            UpdatedOnUtc = command.UpdatedOnUtc
        };
    }
}
