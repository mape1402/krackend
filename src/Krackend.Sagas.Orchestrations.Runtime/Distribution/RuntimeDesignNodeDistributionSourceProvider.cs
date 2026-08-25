using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Provides runtime distribution sources from stored design nodes and configured defaults.
/// </summary>
public sealed class RuntimeDesignNodeDistributionSourceProvider : IControlPlaneDistributionSourceProvider
{
    private readonly IRuntimeDesignNodeRepository _repository;
    private readonly RuntimeDistributionOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDesignNodeDistributionSourceProvider"/> class.
    /// </summary>
    public RuntimeDesignNodeDistributionSourceProvider(
        IRuntimeDesignNodeRepository repository,
        IOptions<RuntimeDistributionOptions> options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ControlPlaneDistributionSource> GetAll()
    {
        var sources = new Dictionary<string, ControlPlaneDistributionSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in _options.ControlPlanes.Where(x => x.IsEnabled))
        {
            if (!string.IsNullOrWhiteSpace(source.Key))
            {
                sources[source.Key] = source;
            }
        }

        foreach (var designNode in _repository.GetEnabled())
        {
            if (!string.IsNullOrWhiteSpace(designNode.Key))
            {
                sources[designNode.Key] = ToSource(designNode);
            }
        }

        return sources.Values
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Key)
            .ToArray();
    }

    /// <inheritdoc />
    public ControlPlaneDistributionSource GetByKey(string sourceKey)
        => GetAll().FirstOrDefault(x => string.Equals(x.Key, sourceKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Control-plane source '{sourceKey}' was not registered.");

    /// <inheritdoc />
    public ControlPlaneDistributionSource GetByClientId(string clientId)
        => GetAll().FirstOrDefault(x => string.Equals(x.ClientId, clientId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Control-plane source for client id '{clientId}' was not registered.");

    private static ControlPlaneDistributionSource ToSource(RuntimeDesignNode designNode)
        => new()
        {
            Key = designNode.Key,
            Name = designNode.Name,
            EndpointBaseUri = designNode.EndpointBaseUri,
            RemoteRuntimeNodeId = designNode.RemoteRuntimeNodeId,
            ClientId = designNode.ClientId,
            SecretReference = designNode.SecretReference,
            IsEnabled = designNode.IsEnabled
        };
}
