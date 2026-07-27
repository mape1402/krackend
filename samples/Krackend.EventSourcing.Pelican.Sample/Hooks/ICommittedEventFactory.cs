namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public interface ICommittedEventFactory<in TRequest, in TEntity>
{
    ValueTask<object?> CreateAsync(
        TRequest request,
        TEntity entity,
        CancellationToken cancellationToken = default);
}
