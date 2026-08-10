namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Internal write-side contract used by runtime infrastructure and interceptors to control scoped metadata.
/// </summary>
public interface IOrchestratorMetadataWriter
{
    /// <summary>
    /// Sets the immutable metadata object for the current execution scope.
    /// </summary>
    /// <param name="metadata">Metadata to expose through the accessor.</param>
    void Set(OrchestratorMessageMetadata metadata);

    /// <summary>
    /// Clears the current metadata from the execution scope.
    /// </summary>
    void Clear();
}
