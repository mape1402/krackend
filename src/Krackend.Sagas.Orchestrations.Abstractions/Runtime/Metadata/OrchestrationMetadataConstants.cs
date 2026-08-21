namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Shared metadata keys used by orchestration transports.
/// </summary>
public static class OrchestrationMetadataConstants
{
    /// <summary>
    /// Gets the metadata key used to carry orchestration message metadata.
    /// </summary>
    public const string OrchestrationMessageMetadataKey = "Krackend.Sagas.Orchestrations.Message.Metadata";

    /// <summary>
    /// Gets the metadata key used to carry orchestration execution result metadata.
    /// </summary>
    public const string OrchestrationExecutionResultMetadataKey = "Krackend.Sagas.Orchestrations.Execution.Result.Metadata";
}
