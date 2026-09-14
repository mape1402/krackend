using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas.Resolution;

/// <summary>
/// Resolves schema contracts from Atlas when Atlas schema lookup endpoints are available.
/// </summary>
public sealed class AtlasSchemaContractResolver : ISchemaContractResolver
{
    private readonly AtlasSchemaRegistryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtlasSchemaContractResolver"/> class.
    /// </summary>
    public AtlasSchemaContractResolver(IOptions<AtlasSchemaRegistryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderKey => string.IsNullOrWhiteSpace(_options.ProviderKey) ? "atlas" : _options.ProviderKey;

    /// <inheritdoc />
    public Task<SchemaContractResolutionResult> ResolveAsync(
        SchemaContractResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return Task.FromResult(SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotConfigured,
                "Atlas schema registry adapter is installed but disabled."));
        }

        if (_options.BaseUri is null)
        {
            return Task.FromResult(SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotConfigured,
                "Atlas schema registry adapter requires BaseUri before resolving contracts."));
        }

        return Task.FromResult(SchemaContractResolutionResult.Failed(
            SchemaContractResolutionStatus.NotConfigured,
            "Atlas schema lookup endpoint is not available yet. The adapter is ready for plug and play wiring once Atlas exposes it."));
    }
}
