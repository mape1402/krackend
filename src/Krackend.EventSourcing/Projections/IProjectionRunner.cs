namespace Krackend.EventSourcing.Projections;

/// <summary>
/// Runs a projection from committed events.
/// </summary>
public interface IProjectionRunner
{
    /// <summary>
    /// Runs one projection batch.
    /// </summary>
    Task<int> RunBatchAsync(
        string projectionName,
        string streamName,
        int maxCount,
        CancellationToken cancellationToken = default);
}
