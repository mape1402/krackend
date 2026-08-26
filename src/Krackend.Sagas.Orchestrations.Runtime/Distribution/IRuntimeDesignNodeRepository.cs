using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Stores design/control-plane nodes registered by a runtime node.
/// </summary>
public interface IRuntimeDesignNodeRepository
{
    /// <summary>
    /// Returns all registered design nodes.
    /// </summary>
    Task<IReadOnlyCollection<RuntimeDesignNode>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns enabled design nodes for synchronous runtime distribution lookups.
    /// </summary>
    IReadOnlyCollection<RuntimeDesignNode> GetEnabled();

    /// <summary>
    /// Returns one design node by id.
    /// </summary>
    Task<RuntimeDesignNode> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one design node by runtime-local key.
    /// </summary>
    Task<RuntimeDesignNode> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one design node by inbound client id.
    /// </summary>
    Task<RuntimeDesignNode> GetByInboundClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates one design node.
    /// </summary>
    Task UpsertAsync(RuntimeDesignNode designNode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables one design node.
    /// </summary>
    Task SetEnabledAsync(Id id, bool isEnabled, CancellationToken cancellationToken = default);
}
