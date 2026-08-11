namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Provides scoped access to metadata captured before business execution.
/// </summary>
public interface IOrchestrationClientMetadataAccessor
{
    /// <summary>
    /// Gets the current orchestration client metadata.
    /// </summary>
    OrchestrationClientMetadata Current { get; }
}
