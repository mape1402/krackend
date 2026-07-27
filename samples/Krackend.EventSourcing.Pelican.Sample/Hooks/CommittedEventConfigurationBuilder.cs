using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class CommittedEventConfigurationBuilder
{
    private readonly IServiceCollection _services;

    public CommittedEventConfigurationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public CommittedEventConfigurationBuilder Map<TRequest, TEntity, TEvent>()
    {
        _services.AddScoped<
            ICommittedEventMapper<TRequest, TEntity>,
            OctoMapCommittedEventMapper<TRequest, TEntity, TEvent>>();

        return this;
    }
}
