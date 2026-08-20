namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Stores orchestration instance metadata in the current scope.
/// </summary>
public interface IInstanceMetadataSetter
{
    /// <summary>
    /// Sets the current orchestration instance metadata.
    /// </summary>
    void Set(InstanceMetadata metadata);
}
