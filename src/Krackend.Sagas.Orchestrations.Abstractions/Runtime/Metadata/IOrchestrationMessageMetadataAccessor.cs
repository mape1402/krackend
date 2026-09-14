namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Provides the orchestration message metadata for the current scope.
/// </summary>
public interface IOrchestrationMessageMetadataAccessor
{
    /// <summary>
    /// Gets the orchestration message metadata for the current scope.
    /// </summary>
    /// <returns>The current orchestration message metadata.</returns>
    OrchestrationMessageMetadata Get();
}
