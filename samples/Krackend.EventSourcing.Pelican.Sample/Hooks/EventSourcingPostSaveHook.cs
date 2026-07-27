using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Pelican.Sample.Hooks;

public sealed class EventSourcingPostSaveHook<TRequest, TEntity>
    : ICommandHandlerHook<TRequest, TEntity>
{
    private readonly ICommandStreamResolver<TRequest> _streamResolver;
    private readonly ICommittedEventFactory<TRequest, TEntity> _eventFactory;
    private readonly IEventStore _eventStore;

    public EventSourcingPostSaveHook(
        ICommandStreamResolver<TRequest> streamResolver,
        ICommittedEventFactory<TRequest, TEntity> eventFactory,
        IEventStore eventStore)
    {
        _streamResolver = streamResolver;
        _eventFactory = eventFactory;
        _eventStore = eventStore;
    }

    public async ValueTask PostSaveEntityAsync(
        CommandHookContext<TRequest, TEntity> context,
        CancellationToken cancellationToken = default)
    {
        if (context.Entity is null)
            throw new InvalidOperationException("Entity must be available after save.");

        var stream = _streamResolver.Resolve(context.Request);
        var existingEvents = await _eventStore.LoadAsync(stream.Name, stream.Id, cancellationToken);
        var events = await _eventFactory.CreateAsync(context.Request, context.Entity, cancellationToken);

        if (events.Count == 0)
            return;

        await _eventStore.AppendAsync(stream.Name, stream.Id, existingEvents.Count, events, cancellationToken);
    }
}
