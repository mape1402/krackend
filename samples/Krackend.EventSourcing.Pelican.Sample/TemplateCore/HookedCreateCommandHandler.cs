using Pelican.Mediator;

namespace Krackend.EventSourcing.Pelican.Sample.TemplateCore;

public abstract class HookedCreateCommandHandler<TRequest, TResponse, TEntity>
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TEntity : class
{
    private readonly IEnumerable<ICommandHandlerHook<TRequest, TEntity>> _hooks;

    protected HookedCreateCommandHandler(IEnumerable<ICommandHandlerHook<TRequest, TEntity>> hooks)
    {
        _hooks = hooks;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default)
    {
        var context = new CommandHookContext<TRequest, TEntity>(request);

        await ValidateWithHooksAsync(context, cancellationToken);
        context.Entity = await MapToEntityWithHooksAsync(context, cancellationToken);
        await SaveEntityWithHooksAsync(context, cancellationToken);

        return await MapToResponseAsync(request, context.Entity, cancellationToken);
    }

    protected virtual ValueTask ValidateAsync(TRequest request, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    protected abstract ValueTask<TEntity> MapToEntityAsync(TRequest request, CancellationToken cancellationToken);

    protected abstract Task SaveEntityAsync(TRequest request, TEntity entity, CancellationToken cancellationToken);

    protected abstract ValueTask<TResponse> MapToResponseAsync(TRequest request, TEntity entity, CancellationToken cancellationToken);

    private async ValueTask ValidateWithHooksAsync(
        CommandHookContext<TRequest, TEntity> context,
        CancellationToken cancellationToken)
    {
        foreach (var hook in _hooks)
            await hook.PreValidationAsync(context, cancellationToken);

        await ValidateAsync(context.Request, cancellationToken);

        foreach (var hook in _hooks)
            await hook.PostValidationAsync(context, cancellationToken);
    }

    private async ValueTask<TEntity> MapToEntityWithHooksAsync(
        CommandHookContext<TRequest, TEntity> context,
        CancellationToken cancellationToken)
    {
        foreach (var hook in _hooks)
            await hook.PreMapToEntityAsync(context, cancellationToken);

        var entity = await MapToEntityAsync(context.Request, cancellationToken);
        context.Entity = entity;

        foreach (var hook in _hooks)
            await hook.PostMapToEntityAsync(context, cancellationToken);

        return entity;
    }

    private async Task SaveEntityWithHooksAsync(
        CommandHookContext<TRequest, TEntity> context,
        CancellationToken cancellationToken)
    {
        if (context.Entity is null)
            throw new InvalidOperationException("Entity must be mapped before it can be saved.");

        foreach (var hook in _hooks)
            await hook.PreSaveEntityAsync(context, cancellationToken);

        await SaveEntityAsync(context.Request, context.Entity, cancellationToken);

        foreach (var hook in _hooks)
            await hook.PostSaveEntityAsync(context, cancellationToken);
    }
}
