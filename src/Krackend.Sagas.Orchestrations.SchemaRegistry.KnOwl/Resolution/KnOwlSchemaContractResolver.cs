using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Resolution;

/// <summary>
/// Resolves schema contracts from deployed KnOwl Control Plane contract artifacts.
/// </summary>
public sealed class KnOwlSchemaContractResolver : ISchemaContractResolver
{
    private const string DefaultProviderKey = "knowl";
    private readonly IKnOwlControlPlaneContractCatalogClient _catalogClient;
    private readonly KnOwlSchemaRegistryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnOwlSchemaContractResolver"/> class.
    /// </summary>
    public KnOwlSchemaContractResolver(
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
    public async Task<SchemaContractResolutionResult> ResolveAsync(
        SchemaContractResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotConfigured,
                "KnOwl schema registry adapter is installed but disabled.");
        }

        if (_options.BaseUri is null)
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotConfigured,
                "KnOwl schema registry adapter requires BaseUri before resolving contracts.");
        }

        var reference = request.Reference ?? new SchemaContractReference();
        if (string.IsNullOrWhiteSpace(reference.ContractKey))
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.Invalid,
                "KnOwl schema contract key is required.");
        }

        if (!TryMapArtifactType(reference.Kind, out var artifactType))
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.Invalid,
                $"Schema contract kind '{reference.Kind}' is not supported by KnOwl deployed contract artifacts.");
        }

        var contractVersion = reference.ContractVersion;
        var catalogResult = string.IsNullOrWhiteSpace(contractVersion) || contractVersion == "0.0.0"
            ? await ResolveLatestAsync(artifactType, reference, cancellationToken)
            : await _catalogClient.GetExactAsync(artifactType, reference.ContractKey, contractVersion, cancellationToken);

        return MapResolutionResult(reference, catalogResult);
    }

    private async Task<KnOwlContractCatalogResult> ResolveLatestAsync(
        ContractArtifactType artifactType,
        SchemaContractReference reference,
        CancellationToken cancellationToken)
    {
        if (!_options.AllowLatestVersionResolution)
        {
            return KnOwlContractCatalogResult.Failed(
                KnOwlContractCatalogStatus.Invalid,
                "KnOwl schema contract version is required. Enable latest-version resolution only when mutable latest binding is acceptable.");
        }

        return await _catalogClient.GetLatestAsync(artifactType, reference.ContractKey, cancellationToken);
    }

    private SchemaContractResolutionResult MapResolutionResult(
        SchemaContractReference reference,
        KnOwlContractCatalogResult catalogResult)
    {
        if (catalogResult is null)
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.Unavailable,
                "KnOwl Control Plane catalog returned no result.");
        }

        if (catalogResult.Status != KnOwlContractCatalogStatus.Found)
        {
            return SchemaContractResolutionResult.Failed(
                MapStatus(catalogResult.Status),
                catalogResult.Message);
        }

        var contract = catalogResult.Contract;
        if (contract is null)
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotFound,
                "KnOwl Control Plane catalog returned no contract artifact.");
        }

        if (!string.IsNullOrWhiteSpace(contract.SourceStatus) &&
            !string.Equals(contract.SourceStatus, "Deployed", StringComparison.OrdinalIgnoreCase))
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.Invalid,
                $"KnOwl contract artifact '{contract.Topic}' v{contract.VersionNumber} is not deployed.");
        }

        if (string.IsNullOrWhiteSpace(contract.PayloadSchemaJson))
        {
            return SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.Invalid,
                $"KnOwl contract artifact '{contract.Topic}' v{contract.VersionNumber} does not contain a payload schema.");
        }

        var snapshot = new SchemaContractSnapshot
        {
            Reference = reference with
            {
                ProviderKey = ProviderKey,
                ContractId = contract.Id == Guid.Empty ? reference.ContractId : contract.Id.ToString(),
                ContractKey = string.IsNullOrWhiteSpace(contract.Topic) ? reference.ContractKey : contract.Topic,
                ContractVersion = string.IsNullOrWhiteSpace(contract.VersionNumber) ? reference.ContractVersion : contract.VersionNumber
            },
            SchemaFormat = string.IsNullOrWhiteSpace(_options.SchemaFormat) ? "ButterMorph" : _options.SchemaFormat,
            SchemaJson = contract.PayloadSchemaJson,
            ContentHash = contract.ContentHash ?? string.Empty,
            SourceArtifactId = contract.Id == Guid.Empty ? string.Empty : contract.Id.ToString(),
            ResolvedBy = ProviderKey,
            ResolvedAtUtc = DateTimeOffset.UtcNow
        };

        return SchemaContractResolutionResult.Resolved(snapshot);
    }

    private static SchemaContractResolutionStatus MapStatus(KnOwlContractCatalogStatus status)
        => status switch
        {
            KnOwlContractCatalogStatus.NotFound => SchemaContractResolutionStatus.NotFound,
            KnOwlContractCatalogStatus.Invalid => SchemaContractResolutionStatus.Invalid,
            KnOwlContractCatalogStatus.Unavailable => SchemaContractResolutionStatus.Unavailable,
            _ => SchemaContractResolutionStatus.Unavailable
        };

    private static bool TryMapArtifactType(SchemaContractKind contractKind, out ContractArtifactType artifactType)
    {
        switch (contractKind)
        {
            case SchemaContractKind.Event:
                artifactType = ContractArtifactType.Event;
                return true;
            case SchemaContractKind.CommandRequest:
            case SchemaContractKind.Command:
                artifactType = ContractArtifactType.CommandRequest;
                return true;
            case SchemaContractKind.CommandResponse:
                artifactType = ContractArtifactType.CommandReply;
                return true;
            default:
                artifactType = default;
                return false;
        }
    }
}
