using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class CommittedEventConfigurationBuilder
{
    private readonly IServiceCollection _services;

    public CommittedEventConfigurationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public CommittedEventMapExpression<TEvent> CreateMultiMap<TEvent>()
    {
        return new CommittedEventMapExpression<TEvent>(_services);
    }
}
