namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Provides read-only access to the current orchestrator metadata in the execution scope.
/// </summary>
public interface IOrchestratorMetadataAccessor
{
    /// <summary>
    /// Gets the current orchestrator metadata, or null when the current scope does not carry orchestration context.
    /// </summary>
    OrchestratorMessageMetadata Current { get; }
}
