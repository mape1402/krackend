namespace Krackend.EventSourcing.Projections;

/// <summary>
/// Stores projection checkpoints.
/// </summary>
public interface ICheckpointStore
{
    /// <summary>
    /// Gets the last processed global position.
    /// </summary>
    Task<long> GetLastPositionAsync(
        string projectionName,
        string streamName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the last processed global position.
    /// </summary>
    Task SaveAsync(
        string projectionName,
        string streamName,
        long globalPosition,
        CancellationToken cancellationToken = default);
}
