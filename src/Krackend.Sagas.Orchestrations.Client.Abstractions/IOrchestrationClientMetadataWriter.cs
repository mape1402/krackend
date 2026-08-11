namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Stores orchestration client metadata in the current execution scope.
/// </summary>
public interface IOrchestrationClientMetadataWriter
{
    /// <summary>
    /// Stores metadata for the current execution scope.
    /// </summary>
    void Set(OrchestrationClientMetadata metadata);

    /// <summary>
    /// Clears metadata from the current execution scope.
    /// </summary>
    void Clear();
}
