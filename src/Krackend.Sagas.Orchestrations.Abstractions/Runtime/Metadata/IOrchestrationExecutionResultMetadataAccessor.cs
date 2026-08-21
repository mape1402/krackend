namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Provides the orchestration execution result metadata for the current scope.
/// </summary>
public interface IOrchestrationExecutionResultMetadataAccessor
{
    /// <summary>
    /// Gets the orchestration execution result metadata for the current scope.
    /// </summary>
    /// <returns>The current execution result metadata, or null when none has been set.</returns>
    OrchestrationExecutionResultMetadata Get();
}
