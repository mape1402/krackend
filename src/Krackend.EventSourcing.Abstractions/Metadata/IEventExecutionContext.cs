namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Provides correlation metadata for events committed in the current execution flow.
/// </summary>
public interface IEventExecutionContext
{
    /// <summary>
    /// Gets the identifier that correlates all work in the same flow.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the identifier of the request, message, or event that caused the current work.
    /// </summary>
    string? CausationId { get; }

    /// <summary>
    /// Gets the current user identifier, when one is available.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current tenant identifier, when one is available.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the component or channel that originated the current flow.
    /// </summary>
    string? Source { get; }
}
