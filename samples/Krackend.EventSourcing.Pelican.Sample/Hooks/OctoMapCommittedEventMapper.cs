using OctoMap;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class OctoMapCommittedEventMapper<TRequest, TEntity, TEvent>
    : ICommittedEventMapper<TRequest, TEntity>
{
    private readonly IOctoMapper _mapper;

    public OctoMapCommittedEventMapper(IOctoMapper mapper)
    {
        _mapper = mapper;
    }

    public object? Map(TRequest request, TEntity entity)
    {
        return _mapper.Map<TRequest, TEntity, TEvent>(request, entity);
    }
}
