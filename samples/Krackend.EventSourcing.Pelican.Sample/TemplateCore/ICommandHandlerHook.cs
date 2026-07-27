namespace Krackend.EventSourcing.Pelican.Sample.TemplateCore;

public interface ICommandHandlerHook<TRequest, TEntity>
{
    ValueTask PreValidationAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask PostValidationAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask PreMapToEntityAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask PostMapToEntityAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask PreSaveEntityAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask PostSaveEntityAsync(CommandHookContext<TRequest, TEntity> context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
