namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.Logging;
using global::Pigeon.Messaging.Consuming.Dispatching;
using System.Text.Json.Nodes;

/// <summary>
/// Converts a terminal Pigeon rejection for an orchestrated operation into its
/// corresponding orchestration failure reply.
/// </summary>
/// <remarks>
/// This observer deliberately only reacts to <see cref="PigeonConsumeDecision.Reject"/>.
/// Retry, defer, skip, and already-processed decisions retain Pigeon's normal
/// semantics and must not complete an orchestration task.
/// </remarks>
internal sealed class KrackendPigeonConsumeDecisionObserver : IConsumeDecisionObserver
{
    private const string DecisionMetadataKey = "Krackend.Pigeon.Decision.Metadata";
    private const string ReplyMetadataKey = "Krackend.Pigeon.Reply.Metadata";

    private readonly IOrchestrationClientPublisher _publisher;
    private readonly IOrchestrationMessageMetadataAccessor _messageMetadataAccessor;
    private readonly IOrchestrationMessageMetadataSetter _messageMetadataSetter;
    private readonly IOrchestrationExecutionResultMetadataAccessor _resultMetadataAccessor;
    private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;
    private readonly IOrchestrationPropagationMetadataAccessor _propagationMetadataAccessor;
    private readonly IOrchestrationPropagationMetadataSetter _propagationMetadataSetter;
    private readonly ILogger _logger;
    private readonly PigeonPropagationMetadataMapper _propagationMetadataMapper = new();

    public KrackendPigeonConsumeDecisionObserver(
        IOrchestrationClientPublisher publisher,
        IOrchestrationMessageMetadataAccessor messageMetadataAccessor,
        IOrchestrationMessageMetadataSetter messageMetadataSetter,
        IOrchestrationExecutionResultMetadataAccessor resultMetadataAccessor,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter,
        IOrchestrationPropagationMetadataAccessor propagationMetadataAccessor,
        IOrchestrationPropagationMetadataSetter propagationMetadataSetter,
        ILogger logger)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _messageMetadataAccessor = messageMetadataAccessor ?? throw new ArgumentNullException(nameof(messageMetadataAccessor));
        _messageMetadataSetter = messageMetadataSetter ?? throw new ArgumentNullException(nameof(messageMetadataSetter));
        _resultMetadataAccessor = resultMetadataAccessor ?? throw new ArgumentNullException(nameof(resultMetadataAccessor));
        _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
        _propagationMetadataAccessor = propagationMetadataAccessor ?? throw new ArgumentNullException(nameof(propagationMetadataAccessor));
        _propagationMetadataSetter = propagationMetadataSetter ?? throw new ArgumentNullException(nameof(propagationMetadataSetter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Result.Decision != PigeonConsumeDecision.Reject)
        {
            _logger.LogDebug("Ignoring non-terminal Pigeon consume decision {Decision}.", observation.Result.Decision);
            return;
        }

        if (!TryGetOrchestrationMetadata(observation.Context, out var messageMetadata))
        {
            _logger.LogDebug("Ignoring terminal Pigeon consume decision because the message is not orchestrated.");
            return;
        }

        if (messageMetadata.ReplyAddress is null)
        {
            _logger.LogDebug(
                "Ignoring terminal Pigeon consume decision without a reply address. {SagaId} {OrchestrationInstanceId} {TaskExecutionId} {Attempt}",
                messageMetadata.SagaId,
                messageMetadata.OrchestrationInstanceId,
                messageMetadata.TaskExecutionId,
                messageMetadata.Attempt);
            return;
        }

        var previousMessageMetadata = _messageMetadataAccessor.Get();
        var previousResultMetadata = _resultMetadataAccessor.Get();
        var previousPropagationMetadata = _propagationMetadataAccessor.Get();
        var resultMetadata = CreateRejectedResult(observation);
        var propagationMetadata = _propagationMetadataMapper.Capture(observation.Context);

        try
        {
            _logger.LogInformation(
                "Received terminal Pigeon rejection. {SagaId} {OrchestrationInstanceId} {TaskExecutionId} {Attempt} {Outcome} {ErrorCode}",
                messageMetadata.SagaId,
                messageMetadata.OrchestrationInstanceId,
                messageMetadata.TaskExecutionId,
                messageMetadata.Attempt,
                observation.Result.Outcome?.Kind,
                resultMetadata.ErrorCode);

            _messageMetadataSetter.Set(messageMetadata);
            _resultMetadataSetter.Set(resultMetadata);
            _propagationMetadataSetter.Set(propagationMetadata);

            await _publisher.PublishAsync(null!, messageMetadata.ReplyAddress, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Published orchestration failure reply for rejected Pigeon consumption. {SagaId} {OrchestrationInstanceId} {TaskExecutionId} {Attempt} {ErrorCode}",
                messageMetadata.SagaId,
                messageMetadata.OrchestrationInstanceId,
                messageMetadata.TaskExecutionId,
                messageMetadata.Attempt,
                resultMetadata.ErrorCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish orchestration failure reply for rejected Pigeon consumption. {SagaId} {OrchestrationInstanceId} {TaskExecutionId} {Attempt}",
                messageMetadata.SagaId,
                messageMetadata.OrchestrationInstanceId,
                messageMetadata.TaskExecutionId,
                messageMetadata.Attempt);
            throw;
        }
        finally
        {
            _messageMetadataSetter.Set(previousMessageMetadata);
            if (previousResultMetadata is null)
            {
                _resultMetadataSetter.Clear();
            }
            else
            {
                _resultMetadataSetter.Set(previousResultMetadata);
            }

            _propagationMetadataSetter.Set(previousPropagationMetadata);
        }
    }

    private static bool TryGetOrchestrationMetadata(ConsumeContext context, out OrchestrationMessageMetadata metadata)
    {
        try
        {
            metadata = context.GetMetadata<OrchestrationMessageMetadata>(
                OrchestrationMetadataConstants.OrchestrationMessageMetadataKey);
            return metadata is not null;
        }
        catch
        {
            metadata = null!;
            return false;
        }
    }

    private static OrchestrationExecutionResultMetadata CreateRejectedResult(ConsumeDecisionObservation observation)
    {
        var failure = observation.Result.Outcome?.Failure;
        var result = new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Rejected",
            ErrorCode = failure?.Code ?? "Pigeon.Consume.Rejected",
            ErrorMessage = failure?.Message ?? observation.Result.Reason,
            ErrorType = failure?.Type,
            // Pigeon supplies the transport's typed retryability verdict. When
            // it has no failure detail, fail closed so a terminal rejection is
            // never mistaken for a timeout or an implicit retry request.
            IsRetryableCandidate = failure?.IsRetryable ?? false,
            CompletedOnUtc = DateTime.UtcNow
        };

        AddMetadataGroup(result.Metadata, DecisionMetadataKey, observation.Result.Metadata);
        AddMetadataGroup(result.Metadata, ReplyMetadataKey, observation.Context.ReplyMetadata);
        return result;
    }

    private static void AddMetadataGroup(
        IDictionary<string, JsonNode> target,
        string key,
        IEnumerable<KeyValuePair<string, string>> metadata)
    {
        if (metadata is null)
        {
            return;
        }

        var values = new JsonObject();
        foreach (var item in metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                values[item.Key] = JsonValue.Create(item.Value);
            }
        }

        if (values.Count > 0)
        {
            target[key] = values;
        }
    }
}
