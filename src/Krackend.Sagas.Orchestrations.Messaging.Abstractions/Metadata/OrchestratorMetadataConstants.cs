namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Defines metadata constants shared by runtime messaging adapters.
/// </summary>
public static class OrchestratorMetadataConstants
{
    /// <summary>
    /// Metadata entry used to store the full orchestrator metadata object in broker envelopes.
    /// </summary>
    public const string MetadataKey = "Orchestrator.Metadata";
}
