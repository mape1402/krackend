using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Represents one page of runtime artifacts.
/// </summary>
public sealed record RuntimeArtifactPage(
    IReadOnlyCollection<RuntimeOrchestrationArtifact> Items,
    RuntimeArtifactPageCursor NextCursor,
    bool HasMore);
