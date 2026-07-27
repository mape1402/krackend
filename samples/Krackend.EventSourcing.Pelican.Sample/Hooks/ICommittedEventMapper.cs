namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public interface ICommittedEventMapper<in TRequest, in TEntity>
{
    object? Map(TRequest request, TEntity entity);
}
