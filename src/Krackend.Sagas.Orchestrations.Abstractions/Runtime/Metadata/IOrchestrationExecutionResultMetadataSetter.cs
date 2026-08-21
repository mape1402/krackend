namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Updates the orchestration execution result metadata for the current scope.
/// </summary>
public interface IOrchestrationExecutionResultMetadataSetter
{
    /// <summary>
    /// Sets the orchestration execution result metadata for the current scope.
    /// </summary>
    /// <param name="metadata">Execution result metadata to expose in the current scope.</param>
    void Set(OrchestrationExecutionResultMetadata metadata);

    /// <summary>
    /// Clears the orchestration execution result metadata for the current scope.
    /// </summary>
    void Clear();
}
