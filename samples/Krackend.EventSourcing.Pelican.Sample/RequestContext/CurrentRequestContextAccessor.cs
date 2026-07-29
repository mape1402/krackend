namespace Krackend.EventSourcing.Pelican.Sample.RequestContext;

public sealed class CurrentRequestContextAccessor : ICurrentRequestContextAccessor
{
    private static readonly AsyncLocal<CurrentRequestContext?> CurrentContext = new();

    public CurrentRequestContext? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }
}
