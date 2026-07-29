using Krackend.EventSourcing.Metadata;

namespace Krackend.EventSourcing.Pelican.Sample.RequestContext;

public sealed class CurrentRequestEventExecutionContext : IEventExecutionContext
{
    private readonly ICurrentRequestContextAccessor _requestContextAccessor;

    public CurrentRequestEventExecutionContext(ICurrentRequestContextAccessor requestContextAccessor)
    {
        _requestContextAccessor = requestContextAccessor;
    }

    public string? CorrelationId => _requestContextAccessor.Current?.CorrelationId;

    public string? CausationId => _requestContextAccessor.Current?.CausationId;

    public string? UserId => _requestContextAccessor.Current?.UserId;

    public string? TenantId => _requestContextAccessor.Current?.TenantId;

    public string? Source => _requestContextAccessor.Current?.Source;
}
