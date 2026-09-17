namespace Krackend.Sagas.Orchestrations.Tests.Runtime.RealInfrastructure;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed record RealMessagingServiceInvocation(
    string Topic,
    JsonNode? Payload,
    OrchestrationMessageMetadata MessageMetadata);
