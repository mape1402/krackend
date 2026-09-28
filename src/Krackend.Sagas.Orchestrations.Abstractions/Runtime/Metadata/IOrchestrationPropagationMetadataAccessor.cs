namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Provides access to propagation metadata available in the current orchestration scope.
/// </summary>
public interface IOrchestrationPropagationMetadataAccessor
{
    /// <summary>
    /// Gets the propagation metadata captured for the current scope.
    /// </summary>
    /// <returns>The current propagation metadata envelope.</returns>
    OrchestrationPropagationMetadata Get();
}
