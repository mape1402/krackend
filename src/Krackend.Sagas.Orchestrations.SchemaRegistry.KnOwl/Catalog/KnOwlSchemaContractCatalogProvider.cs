using KnOwl.Contracts.Artifacts;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Exposes deployed KnOwl Control Plane contract artifacts as schema registry catalog items.
/// </summary>
public sealed class KnOwlSchemaContractCatalogProvider : ISchemaContractCatalogProvider
{
    private const string DefaultProviderKey = "knowl";
    private readonly IKnOwlControlPlaneContractCatalogClient _catalogClient;
    private readonly KnOwlSchemaRegistryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnOwlSchemaContractCatalogProvider"/> class.
    /// </summary>
    /// <param name="options">KnOwl schema registry options.</param>
    /// <param name="catalogClient">KnOwl Control Plane catalog client.</param>
    public KnOwlSchemaContractCatalogProvider(
        IOptions<KnOwlSchemaRegistryOptions> options,
        IKnOwlControlPlaneContractCatalogClient catalogClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderKey => string.IsNullOrWhiteSpace(_options.ProviderKey) ? DefaultProviderKey : _options.ProviderKey;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
        SchemaContractCatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new SchemaContractCatalogSearchRequest();
        if (!_options.Enabled || _options.BaseUri is null)
        {
            return Array.Empty<SchemaContractCatalogItem>();
        }

        var catalogResult = await _catalogClient.GetAllDeployedAsync(cancellationToken);
        if (catalogResult?.Status != KnOwlContractCatalogStatus.Found)
        {
            return Array.Empty<SchemaContractCatalogItem>();
        }

        var take = request.Take <= 0 ? 25 : Math.Min(request.Take, 100);
        return catalogResult.Contracts
            .Where(contract => contract is not null)
            .Where(contract => string.Equals(contract.SourceStatus, "Deployed", StringComparison.OrdinalIgnoreCase))
            .Where(contract => HasPayloadSchema(contract))
            .Where(contract => MatchesKind(contract, request.ContractKind))
            .Where(contract => MatchesSearchText(contract, request.SearchText))
            .OrderByDescending(contract => contract.CreatedAtUtc)
            .ThenBy(contract => contract.Topic, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(contract => contract.VersionNumber, StringComparer.OrdinalIgnoreCase)
            .Select(contract => Map(contract, request.ContractKind))
            .GroupBy(item => $"{item.ContractKind}|{item.ContractKey}|{item.ContractVersion}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(take)
            .ToArray();
    }

    private SchemaContractCatalogItem Map(ContractArtifact contract, SchemaContractKind requestedKind)
    {
        var kind = requestedKind is SchemaContractKind.Command or SchemaContractKind.CommandRequest or SchemaContractKind.CommandResponse
            ? requestedKind
            : MapKind(contract.ArtifactType);
        var key = contract.Topic ?? string.Empty;
        var version = contract.VersionNumber ?? string.Empty;

        return new SchemaContractCatalogItem
        {
            ProviderKey = ProviderKey,
            ContractId = contract.Id == Guid.Empty ? string.Empty : contract.Id.ToString(),
            ContractKey = key,
            ContractVersion = version,
            ContractKind = kind,
            ContentHash = contract.ContentHash ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(version) ? key : $"{key} v{version}"
        };
    }

    private static bool MatchesKind(ContractArtifact contract, SchemaContractKind requestedKind)
    {
        return requestedKind switch
        {
            SchemaContractKind.Unspecified => true,
            SchemaContractKind.Event => contract.ArtifactType == ContractArtifactType.Event,
            SchemaContractKind.Command => contract.ArtifactType is ContractArtifactType.Command or ContractArtifactType.CommandRequest,
            SchemaContractKind.CommandRequest => contract.ArtifactType is ContractArtifactType.Command or ContractArtifactType.CommandRequest,
            SchemaContractKind.CommandResponse => contract.ArtifactType is ContractArtifactType.Command or ContractArtifactType.CommandReply,
            _ => false
        };
    }

    private static SchemaContractKind MapKind(ContractArtifactType artifactType)
    {
        return artifactType switch
        {
            ContractArtifactType.Event => SchemaContractKind.Event,
            ContractArtifactType.CommandRequest => SchemaContractKind.CommandRequest,
            ContractArtifactType.CommandReply => SchemaContractKind.CommandResponse,
            ContractArtifactType.Command => SchemaContractKind.Command,
            _ => SchemaContractKind.Unspecified
        };
    }

    private static bool MatchesSearchText(ContractArtifact contract, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var normalized = searchText.Trim();
        return Contains(contract.Topic, normalized) ||
            Contains(contract.VersionNumber, normalized) ||
            Contains(contract.ContentHash, normalized) ||
            Contains(contract.Id == Guid.Empty ? string.Empty : contract.Id.ToString(), normalized);
    }

    private static bool Contains(string value, string searchText)
        => !string.IsNullOrWhiteSpace(value) &&
            value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private static bool HasPayloadSchema(ContractArtifact contract)
        => !string.IsNullOrWhiteSpace(contract.PayloadSchemaJson);
}
