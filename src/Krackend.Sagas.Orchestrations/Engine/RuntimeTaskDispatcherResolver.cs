namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Resolves runtime task dispatchers registered in the host.
/// </summary>
public sealed class RuntimeTaskDispatcherResolver : IRuntimeTaskDispatcherResolver
{
    private readonly IReadOnlyCollection<IRuntimeTaskDispatcher> _dispatchers;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeTaskDispatcherResolver"/> class.
    /// </summary>
    /// <param name="dispatchers">Registered dispatchers.</param>
    public RuntimeTaskDispatcherResolver(IEnumerable<IRuntimeTaskDispatcher> dispatchers)
    {
        _dispatchers = dispatchers?.ToArray() ?? Array.Empty<IRuntimeTaskDispatcher>();
    }

    /// <inheritdoc/>
    public IRuntimeTaskDispatcher Resolve(string taskKind)
        => _dispatchers.FirstOrDefault(x => x.CanDispatch(taskKind));
}
