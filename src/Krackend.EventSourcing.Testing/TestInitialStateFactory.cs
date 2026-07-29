using Krackend.EventSourcing.Core;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides a fixed initial state for tests.
/// </summary>
public sealed class TestInitialStateFactory<TState> : IInitialStateFactory<TState>
{
    private readonly TState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestInitialStateFactory{TState}"/> class.
    /// </summary>
    public TestInitialStateFactory(TState state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public ValueTask<TState> CreateAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_state);
}
