namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Scoped metadata context shared by accessors, writers and messaging interceptors.
/// </summary>
internal sealed class OrchestratorMetadataContext : IOrchestratorMetadataAccessor, IOrchestratorMetadataWriter
{
    /// <inheritdoc/>
    public OrchestratorMessageMetadata Current { get; private set; }

    /// <inheritdoc/>
    public void Set(OrchestratorMessageMetadata metadata)
    {
        Current = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        Current = null;
    }
}
