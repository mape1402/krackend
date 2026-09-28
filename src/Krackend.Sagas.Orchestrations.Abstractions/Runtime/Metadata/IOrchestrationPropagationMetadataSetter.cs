namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Updates propagation metadata available in the current orchestration scope.
/// </summary>
public interface IOrchestrationPropagationMetadataSetter
{
    /// <summary>
    /// Sets the propagation metadata for the current scope.
    /// </summary>
    /// <param name="metadata">Propagation metadata to expose in the current scope.</param>
    void Set(OrchestrationPropagationMetadata metadata);

    /// <summary>
    /// Clears the propagation metadata for the current scope.
    /// </summary>
    void Clear();
}
