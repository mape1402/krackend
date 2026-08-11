using Krackend.Sagas.Orchestrations.Client.Abstractions;

namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Scoped metadata context shared by client services.
/// </summary>
internal sealed class OrchestrationClientMetadataContext : IOrchestrationClientMetadataAccessor, IOrchestrationClientMetadataWriter
{
    /// <inheritdoc/>
    public OrchestrationClientMetadata Current { get; private set; }

    /// <inheritdoc/>
    public void Set(OrchestrationClientMetadata metadata)
    {
        Current = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        Current = null;
    }
}
