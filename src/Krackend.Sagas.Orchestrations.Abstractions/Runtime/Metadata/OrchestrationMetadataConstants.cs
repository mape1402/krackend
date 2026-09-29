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

    /// <summary>
    /// Gets the metadata key used to carry transport-agnostic propagation metadata.
    /// </summary>
    public const string OrchestrationPropagationMetadataKey = "Krackend.Sagas.Orchestrations.Propagation.Metadata";

    /// <summary>
    /// Gets the propagation metadata key used to carry the trigger metadata contract.
    /// </summary>
    public const string TriggerMetadataKey = "Krackend.Sagas.Orchestrations.Trigger.Metadata";

    /// <summary>
    /// Gets the legacy propagation metadata key used to carry the trigger metadata contract.
    /// </summary>
    public const string LegacyTriggerMetadataKey = "trigger_metadata";

    /// <summary>
    /// Gets the execution metadata key used to indicate that the original business payload was null.
    /// </summary>
    public const string OrchestrationPayloadWasNullMetadataKey = "Krackend.Sagas.Orchestrations.Payload.WasNull";
}
