using Pelican.Mediator;

namespace Krackend.EventSourcing.Pelican.Sample.TemplateCore;

public abstract class HookedCreateCommandHandler<TRequest, TResponse, TEntity>
    : HookedEntityCommandHandler<TRequest, TResponse, TEntity>
    where TRequest : IRequest<TResponse>
    where TEntity : class
{
    protected HookedCreateCommandHandler(IEnumerable<ICommandHandlerHook<TRequest, TEntity>> hooks)
        : base(hooks)
    {
    }
}
