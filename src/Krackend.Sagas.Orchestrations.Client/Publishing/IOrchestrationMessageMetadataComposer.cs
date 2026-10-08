namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal interface IOrchestrationMessageMetadataComposer
{
    OrchestrationMessageMetadata ComposeEventMessageMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata,
        OrchestrationTriggerMetadata triggerMetadata,
        OrchestrationReplyAddress triggerAddress);

    OrchestrationPropagationMetadata ComposeEventPropagationMetadata(
        OrchestrationMessageMetadata previousMessageMetadata,
        OrchestrationPropagationMetadata previousPropagationMetadata,
        OrchestrationTriggerMetadata triggerMetadata,
        OrchestrationReplyAddress triggerAddress);
}

