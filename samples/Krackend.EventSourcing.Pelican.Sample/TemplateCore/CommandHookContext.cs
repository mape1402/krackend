namespace Krackend.EventSourcing.Pelican.Sample.TemplateCore;

public sealed class CommandHookContext<TRequest, TEntity>
{
    public CommandHookContext(TRequest request)
    {
        Request = request;
    }

    public TRequest Request { get; }

    public TEntity? Entity { get; set; }
}
