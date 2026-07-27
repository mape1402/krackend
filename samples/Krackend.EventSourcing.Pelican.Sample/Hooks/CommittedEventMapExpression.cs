using Microsoft.Extensions.DependencyInjection;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class CommittedEventMapExpression<TEvent>
{
    private readonly IServiceCollection _services;

    internal CommittedEventMapExpression(IServiceCollection services)
    {
        _services = services;
    }

    public CommittedEventMapSourceExpression<TEvent, TRequest> From<TRequest>()
    {
        return new CommittedEventMapSourceExpression<TEvent, TRequest>(_services);
    }
}

public sealed class CommittedEventMapSourceExpression<TEvent, TRequest>
{
    private readonly IServiceCollection _services;

    internal CommittedEventMapSourceExpression(IServiceCollection services)
    {
        _services = services;
    }

    public CommittedEventMapExpression<TEvent, TRequest, TEntity> From<TEntity>()
    {
        return new CommittedEventMapExpression<TEvent, TRequest, TEntity>(_services);
    }
}

public sealed class CommittedEventMapExpression<TEvent, TRequest, TEntity>
{
    private readonly IServiceCollection _services;

    internal CommittedEventMapExpression(IServiceCollection services)
    {
        _services = services;
    }

    public IServiceCollection ConstructUsing(Func<TRequest, TEntity, TEvent?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return ConstructUsing((request, entity, _) =>
            ValueTask.FromResult(factory(request, entity)));
    }

    public IServiceCollection ConstructUsing(
        Func<TRequest, TEntity, CancellationToken, ValueTask<TEvent?>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _services.AddScoped<ICommittedEventFactory<TRequest, TEntity>>(_ =>
            new DelegateCommittedEventFactory<TRequest, TEntity, TEvent>(factory));

        return _services;
    }
}
