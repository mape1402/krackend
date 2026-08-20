namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Reads the current orchestration instance metadata.
/// </summary>
public interface IInstanceMetadataAccessor
{
    /// <summary>
    /// Gets the current orchestration instance metadata.
    /// </summary>
    InstanceMetadata Get();
}
