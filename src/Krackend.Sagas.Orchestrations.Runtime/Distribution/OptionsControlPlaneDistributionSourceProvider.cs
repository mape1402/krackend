using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Reads runtime control-plane sources from configuration options.
/// </summary>
public sealed class OptionsControlPlaneDistributionSourceProvider : IControlPlaneDistributionSourceProvider
{
    private readonly RuntimeDistributionOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsControlPlaneDistributionSourceProvider"/> class.
    /// </summary>
    public OptionsControlPlaneDistributionSourceProvider(IOptions<RuntimeDistributionOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ControlPlaneDistributionSource> GetAll()
        => _options.ControlPlanes
            .Where(x => x.IsEnabled)
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
            ?? throw new KeyNotFoundException($"Control-plane source for client id '{clientId}' was not registered.");
}
