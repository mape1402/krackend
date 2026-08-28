using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Provides runtime distribution sources from stored design nodes.
/// </summary>
public sealed class RuntimeDesignNodeDistributionSourceProvider : IControlPlaneDistributionSourceProvider
{
    private readonly IRuntimeDesignNodeRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDesignNodeDistributionSourceProvider"/> class.
    /// </summary>
    public RuntimeDesignNodeDistributionSourceProvider(IRuntimeDesignNodeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ControlPlaneDistributionSource> GetAll()
        => _repository.GetEnabled()
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Where(x => !string.IsNullOrWhiteSpace(x.EndpointBaseUri))
            .Where(x => x.DistributionMode is DistributionConnectionMode.RuntimeFetchesFromDesign or DistributionConnectionMode.HybridSync)
            .Where(x => x.OutboundCredentialStatus == ConnectionCredentialStatus.Active)
            .Select(ToSource)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Key)
            .ToArray();

    /// <inheritdoc />
    public ControlPlaneDistributionSource GetByKey(string sourceKey)
        => GetAll().FirstOrDefault(x => string.Equals(x.Key, sourceKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Control-plane source '{sourceKey}' was not registered.");

    /// <inheritdoc />
    public ControlPlaneDistributionSource GetByClientId(string clientId)
        => GetAll().FirstOrDefault(x => string.Equals(x.ClientId, clientId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Control-plane source for outbound client id '{clientId}' was not registered.");

    private static ControlPlaneDistributionSource ToSource(RuntimeDesignNode designNode)
        => new()
        {
            Key = designNode.Key,
            Name = designNode.Name,
            EndpointBaseUri = designNode.EndpointBaseUri,
            RemoteRuntimeNodeId = designNode.RemoteRuntimeNodeId,
            ClientId = designNode.OutboundClientId,
            ProtectedSecret = designNode.ProtectedOutboundSecret,
            KeyId = designNode.OutboundKeyId,
            RequestedScopes = designNode.OutboundRequestedScopes,
            TokenRefreshSkewSeconds = designNode.TokenRefreshSkewSeconds,
            IsEnabled = designNode.IsEnabled
        };
}
