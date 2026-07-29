namespace Krackend.EventSourcing.Pelican.Sample.RequestContext;

public sealed record CurrentRequestContext(
    string CorrelationId,
    string CausationId,
    string UserId,
    string TenantId,
    string Source);
