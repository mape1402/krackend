using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.RuntimeHost.Mongo.Sample.Bootstrap;

internal sealed record HappyPathSeedDefinition(
    SemanticVersion Version,
    Id ArtifactId,
    Id DefinitionId,
    Id VersionId,
    Id StageId,
    Id TriggerId,
    Id InventoryTaskId,
    Id PaymentTaskId,
    Id RegistryProviderId,
    string InventoryTopic,
    string PaymentTopic,
    Id? PaymentStageId = null,
    Id? CompletionStageId = null,
    Id? CompletionTaskId = null,
    string? CompletionTopic = null);
