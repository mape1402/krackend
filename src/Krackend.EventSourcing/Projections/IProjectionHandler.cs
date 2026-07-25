namespace Krackend.EventSourcing.Projections;

/// <summary>
/// Handles events for a projection.
/// </summary>
public interface IProjectionHandler<in TEvent>
{
    /// <summary>
    /// Applies an event to the projection.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
