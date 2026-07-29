namespace Krackend.EventSourcing.Core;

/// <summary>
/// Creates initial state values from a delegate.
/// </summary>
public sealed class DelegateInitialStateFactory<TState> : IInitialStateFactory<TState>
{
    private readonly Func<IServiceProvider, CancellationToken, ValueTask<TState>> _factory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateInitialStateFactory{TState}"/> class.
    /// </summary>
    public DelegateInitialStateFactory(
        Func<IServiceProvider, CancellationToken, ValueTask<TState>> factory,
        IServiceProvider serviceProvider)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public ValueTask<TState> CreateAsync(CancellationToken cancellationToken = default)
    {
        return _factory(_serviceProvider, cancellationToken);
    }
}
