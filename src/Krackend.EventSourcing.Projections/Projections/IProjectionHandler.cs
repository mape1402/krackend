namespace Krackend.EventSourcing.Projections;

/// <summary>
/// Handles events for a projection through an object dispatcher.
/// </summary>
public interface IProjectionHandler
{
    /// <summary>
    /// Gets the event CLR type handled by this projection.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Applies an event to the projection.
    /// </summary>
    Task HandleAsync(object @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles events for a projection.
/// </summary>
public interface IProjectionHandler<in TEvent>
    : IProjectionHandler
{
    /// <inheritdoc />
    Type IProjectionHandler.EventType => typeof(TEvent);

    /// <inheritdoc />
    Task IProjectionHandler.HandleAsync(object @event, CancellationToken cancellationToken)
        => HandleAsync((TEvent)@event, cancellationToken);

    /// <summary>
    /// Applies an event to the projection.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
