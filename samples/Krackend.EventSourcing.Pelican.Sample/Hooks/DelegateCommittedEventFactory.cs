namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class DelegateCommittedEventFactory<TRequest, TEntity, TEvent>
    : ICommittedEventFactory<TRequest, TEntity>
{
    private readonly Func<TRequest, TEntity, CancellationToken, ValueTask<TEvent?>> _factory;

    public DelegateCommittedEventFactory(
        Func<TRequest, TEntity, CancellationToken, ValueTask<TEvent?>> factory)
    {
        _factory = factory;
    }

    public ValueTask<object?> CreateAsync(
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        return CreateCoreAsync(request, entity, cancellationToken);
    }

    private async ValueTask<object?> CreateCoreAsync(
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        return await _factory(request, entity, cancellationToken);
    }
}
