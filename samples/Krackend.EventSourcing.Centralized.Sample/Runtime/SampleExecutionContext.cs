using Krackend.EventSourcing.Metadata;

namespace Krackend.EventSourcing.Centralized.Sample.Runtime;

public sealed class SampleExecutionContext : IEventExecutionContext
{
    public string? CorrelationId { get; set; }

    public string? CausationId { get; set; }

    public string? UserId { get; set; }

    public string? TenantId { get; set; }

    public string? Source { get; set; }
}
