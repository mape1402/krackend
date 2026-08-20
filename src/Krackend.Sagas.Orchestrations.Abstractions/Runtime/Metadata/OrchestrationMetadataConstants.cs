namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Shared metadata keys used by orchestration transports.
/// </summary>
public static class OrchestrationMetadataConstants
{
    /// <summary>
    /// Gets the Pigeon metadata key used to carry orchestration instance metadata.
    /// </summary>
    public const string InstanceMetadataKey = "Krackend.Sagas.Orchestrations.Instance.Metadata";
}
