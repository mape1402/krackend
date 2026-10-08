namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationMessageMetadataComposer : IOrchestrationMessageMetadataComposer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OrchestrationMessageMetadata ComposeEventMessageMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata,
        OrchestrationTriggerMetadata triggerMetadata,
        OrchestrationReplyAddress triggerAddress)
    {
        var effectiveTrigger = CreateEffectiveTriggerMetadata(
            previousMessageMetadata,
            previousPropagationMetadata,
            triggerMetadata,
            triggerAddress);

        return new OrchestrationMessageMetadata
        {
            CorrelationId = effectiveTrigger.CorrelationId
        };
    }

    public OrchestrationPropagationMetadata ComposeEventPropagationMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata,
        OrchestrationTriggerMetadata triggerMetadata,
        OrchestrationReplyAddress triggerAddress)
    {
        var metadata = previousPropagationMetadata?.Clone() ?? new OrchestrationPropagationMetadata();
        var effectiveTrigger = CreateEffectiveTriggerMetadata(
            previousMessageMetadata,
            previousPropagationMetadata,
            triggerMetadata,
            triggerAddress);
        metadata.Items[OrchestrationMetadataConstants.TriggerMetadataKey] = effectiveTrigger.ToJson();
        metadata.Items.Remove(OrchestrationMetadataConstants.LegacyTriggerMetadataKey);

        var origin = CreateOriginMetadata(previousMessageMetadata, previousPropagationMetadata);
        if (origin.HasValues)
        {
            metadata.Items[OrchestrationMetadataConstants.OriginMetadataKey] = origin.ToJson();
        }

        return metadata;
    }

    private static OrchestrationTriggerMetadata CreateEffectiveTriggerMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata,
        OrchestrationTriggerMetadata triggerMetadata,
        OrchestrationReplyAddress triggerAddress)
    {
        var previousTrigger = GetPreviousTriggerMetadata(previousPropagationMetadata);
        var eventId = FirstNonEmpty(triggerMetadata?.EventId, Ulid.NewUlid().ToString());

        return new OrchestrationTriggerMetadata
        {
            CorrelationId = FirstNonEmpty(
                triggerMetadata?.CorrelationId,
                previousTrigger.CorrelationId,
                previousMessageMetadata?.CorrelationId,
                eventId),
            TraceId = FirstNonEmpty(triggerMetadata?.TraceId, previousTrigger.TraceId),
            EventId = eventId,
            EventType = FirstNonEmpty(triggerMetadata?.EventType, GetMessagingTopic(triggerAddress)),
            IdempotencyKey = FirstNonEmpty(triggerMetadata?.IdempotencyKey, eventId),
            AggregateId = triggerMetadata?.AggregateId,
            AggregateType = triggerMetadata?.AggregateType,
            CausationId = FirstNonEmpty(triggerMetadata?.CausationId, previousTrigger.EventId)
        };
    }

    private static OrchestrationOriginMetadata CreateOriginMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata)
    {
        if (!HasOrchestrationOrigin(previousMessageMetadata))
        {
            return new OrchestrationOriginMetadata();
        }

        var previousTrigger = GetPreviousTriggerMetadata(previousPropagationMetadata);
        return new OrchestrationOriginMetadata
        {
            SagaId = previousMessageMetadata.SagaId,
            OrchestrationInstanceId = previousMessageMetadata.OrchestrationInstanceId,
            CorrelationId = FirstNonEmpty(previousMessageMetadata.CorrelationId, previousTrigger.CorrelationId),
            TraceId = previousTrigger.TraceId,
            StageKey = previousMessageMetadata.CurrentStage,
            TaskKeys = previousMessageMetadata.CurrentTasks,
            TaskExecutionId = previousMessageMetadata.TaskExecutionId,
            DispatchId = previousMessageMetadata.DispatchId,
            Attempt = previousMessageMetadata.Attempt,
            Source = "SagaTask"
        };
    }

    private static bool HasOrchestrationOrigin(OrchestrationMessageMetadata metadata)
        => metadata is not null &&
           (!string.IsNullOrWhiteSpace(metadata.SagaId) ||
            !string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) ||
            !string.IsNullOrWhiteSpace(metadata.CurrentStage) ||
            metadata.CurrentTasks is { Length: > 0 } ||
            !string.IsNullOrWhiteSpace(metadata.TaskExecutionId) ||
            !string.IsNullOrWhiteSpace(metadata.DispatchId) ||
            metadata.Attempt > 0);

    private static OrchestrationTriggerMetadata GetPreviousTriggerMetadata(OrchestrationPropagationMetadata metadata)
    {
        if (metadata?.Items is null)
        {
            return new OrchestrationTriggerMetadata();
        }

        if (!metadata.Items.TryGetValue(OrchestrationMetadataConstants.TriggerMetadataKey, out var payload) &&
            !metadata.Items.TryGetValue(OrchestrationMetadataConstants.LegacyTriggerMetadataKey, out payload))
        {
            return new OrchestrationTriggerMetadata();
        }

        return OrchestrationTriggerMetadata.FromJson(payload);
    }

    private static string GetMessagingTopic(OrchestrationReplyAddress address)
    {
        if (address is null ||
            !string.Equals(address.Transport, OrchestrationTransportNames.Messaging, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(address.SettingsPayload))
        {
            return string.Empty;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<MessagingReplyAddressSettings>(
                address.SettingsPayload,
                SerializerOptions);
            return settings?.Topic ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

