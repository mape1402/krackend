namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Warms runtime storage providers before the first orchestration message is processed.
/// </summary>
public interface IRuntimeStorageWarmup
{
    /// <summary>
    /// Initializes provider metadata, connections, and common query paths without mutating runtime state.
    /// </summary>
    Task Warmup(CancellationToken cancellationToken = default);
}
