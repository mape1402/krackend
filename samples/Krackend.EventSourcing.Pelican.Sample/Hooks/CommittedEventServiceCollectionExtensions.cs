using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public static class CommittedEventServiceCollectionExtensions
{
    public static IServiceCollection AddCommittedEvents(
        this IServiceCollection services,
        Action<CommittedEventConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        configure(new CommittedEventConfigurationBuilder(services));
        return services;
    }
}
