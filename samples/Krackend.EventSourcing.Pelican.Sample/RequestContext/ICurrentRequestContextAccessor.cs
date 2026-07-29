namespace Krackend.EventSourcing.Pelican.Sample.RequestContext;

public interface ICurrentRequestContextAccessor
{
    CurrentRequestContext? Current { get; set; }
}
