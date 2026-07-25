using System.Data.Common;

namespace Krackend.EventSourcing.Stores.Relational;

/// <summary>
/// Creates database connections through a configured delegate.
/// </summary>
public sealed class DelegateRelationalEventStoreConnectionFactory : IRelationalEventStoreConnectionFactory
{
    private readonly Func<CancellationToken, ValueTask<DbConnection>> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateRelationalEventStoreConnectionFactory"/> class.
    /// </summary>
    public DelegateRelationalEventStoreConnectionFactory(Func<CancellationToken, ValueTask<DbConnection>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        => _factory(cancellationToken);
}
