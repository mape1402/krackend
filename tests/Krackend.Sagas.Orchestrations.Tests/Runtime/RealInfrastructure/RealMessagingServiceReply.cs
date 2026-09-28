namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed record RealMessagingServiceReply(
    string Topic,
    string Outcome,
    bool Completed,
    string? Error,
    OrchestrationMessageMetadata MessageMetadata,
    OrchestrationPropagationMetadata PropagationMetadata);
