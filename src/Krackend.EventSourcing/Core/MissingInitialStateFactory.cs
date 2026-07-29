using Krackend.EventSourcing.Diagnostics;

namespace Krackend.EventSourcing.Core;

/// <summary>
/// Throws when the initial state for a state type has not been configured.
/// </summary>
public sealed class MissingInitialStateFactory<TState> : IInitialStateFactory<TState>
{
    /// <inheritdoc />
    public ValueTask<TState> CreateAsync(CancellationToken cancellationToken = default)
    {
        throw new InitialStateNotConfiguredException(typeof(TState));
    }
}
