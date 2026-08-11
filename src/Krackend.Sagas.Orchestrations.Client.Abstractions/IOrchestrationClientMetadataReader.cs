namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Reads orchestration metadata from the current incoming transport context.
/// </summary>
public interface IOrchestrationClientMetadataReader
{
    /// <summary>
    /// Reads metadata for the current execution scope.
    /// </summary>
    OrchestrationClientMetadata Read();
}
