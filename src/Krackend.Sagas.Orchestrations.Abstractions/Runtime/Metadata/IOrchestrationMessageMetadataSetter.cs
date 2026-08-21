namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Updates the orchestration message metadata for the current scope.
/// </summary>
public interface IOrchestrationMessageMetadataSetter
{
    /// <summary>
    /// Sets the orchestration message metadata for the current scope.
    /// </summary>
    /// <param name="metadata">Metadata to expose in the current scope.</param>
    void Set(OrchestrationMessageMetadata metadata);
}
