namespace Krackend.EventSourcing.Metadata;

/// <summary>
/// Default immutable event execution context.
/// </summary>
public sealed record EventExecutionContext(
    string? CorrelationId = null,
    string? CausationId = null,
    string? UserId = null,
    string? TenantId = null,
    string? Source = null) : IEventExecutionContext;
