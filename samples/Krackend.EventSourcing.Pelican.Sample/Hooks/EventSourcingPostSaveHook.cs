using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class EventSourcingPostSaveHook<TRequest, TEntity>
    : ICommandHandlerHook<TRequest, TEntity>
{
    private readonly ICommandStreamResolver<TRequest> _streamResolver;
    private readonly ICommittedEventMapper<TRequest, TEntity> _eventMapper;
    private readonly IEventStore _eventStore;

    public EventSourcingPostSaveHook(
        ICommandStreamResolver<TRequest> streamResolver,
        ICommittedEventMapper<TRequest, TEntity> eventMapper,
        IEventStore eventStore)
    {
        _streamResolver = streamResolver;
        _eventMapper = eventMapper;
        _eventStore = eventStore;
    }

    public async ValueTask PostSaveEntityAsync(
        CommandHookContext<TRequest, TEntity> context,
        CancellationToken cancellationToken = default)
    {
        if (context.Entity is null)
            throw new InvalidOperationException("Entity must be available after save.");

        var stream = _streamResolver.Resolve(context.Request);
        var @event = _eventMapper.Map(context.Request, context.Entity);

        if (@event is null)
            return;

        await _eventStore.AppendAsync(stream.Name, stream.Id, ExpectedVersion.Any, [@event], cancellationToken);
    }
}
