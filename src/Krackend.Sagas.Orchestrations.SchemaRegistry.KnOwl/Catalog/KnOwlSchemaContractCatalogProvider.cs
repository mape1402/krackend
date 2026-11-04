using KnOwl.Contracts.Artifacts;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>Exposes deployed KnOwl contracts as schema registry catalog items.</summary>
public sealed class KnOwlSchemaContractCatalogProvider : ISchemaContractCatalogProvider
{
    private readonly IKnOwlControlPlaneContractCatalogClient _catalogClient;
    private readonly KnOwlSchemaRegistryOptions _options;

    /// <summary>Initializes a KnOwl catalog provider.</summary>
    public KnOwlSchemaContractCatalogProvider(
        IOptions<KnOwlSchemaRegistryOptions> options,
        IKnOwlControlPlaneContractCatalogClient catalogClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderKey => string.IsNullOrWhiteSpace(_options.ProviderKey) ? "knowl" : _options.ProviderKey;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
        SchemaContractCatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new SchemaContractCatalogSearchRequest();
        if (!_options.Enabled || _options.BaseUri is null)
            return Array.Empty<SchemaContractCatalogItem>();

        var take = request.Take <= 0 ? 25 : Math.Min(request.Take, 100);
        var text = request.SearchText?.Trim() ?? string.Empty;
        var kind = request.ContractKind;
        if (kind is not (SchemaContractKind.Event or SchemaContractKind.Command or SchemaContractKind.CommandRequest or SchemaContractKind.CommandResponse or SchemaContractKind.Unspecified))
            return Array.Empty<SchemaContractCatalogItem>();

        // A command search already collapses modern commands and legacy requests.
        if (kind is SchemaContractKind.Event or SchemaContractKind.Command or SchemaContractKind.CommandRequest)
        {
            var results = await Search(kind == SchemaContractKind.Event ? "event" : "command", text, take, cancellationToken);
            return results.Where(x => x.HasUsableSchema)
                .Select(x => Map(x, kind))
                .ToArray();
        }

        // The command endpoint represents legacy requests as commands. Fetch requests as well
        // so those representatives are not incorrectly presented as response-capable commands.
        // Each stream is bounded to 100, so any member of the first 100 merged results is
        // present in the first 100 results of its original stream.
        var commandsTask = Search("command", text, 100, cancellationToken);
        var requestsTask = Search("request", text, 100, cancellationToken);
        var repliesTask = Search("reply", text, 100, cancellationToken);
        var eventsTask = kind == SchemaContractKind.Unspecified
            ? Search("event", text, 100, cancellationToken)
            : Task.FromResult<IReadOnlyList<KnOwlContractSummary>>(Array.Empty<KnOwlContractSummary>());
        await Task.WhenAll(commandsTask, requestsTask, repliesTask, eventsTask);
        var requestIds = requestsTask.Result.Select(x => x.Id).ToHashSet();
        var modernCommands = commandsTask.Result.Where(x => !requestIds.Contains(x.Id));
        var candidates = kind == SchemaContractKind.CommandResponse
            ? modernCommands.Concat(repliesTask.Result)
            : eventsTask.Result.Concat(modernCommands).Concat(requestsTask.Result).Concat(repliesTask.Result);

        return candidates.Where(x => x.HasUsableSchema)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.ContractKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ContractKey, StringComparer.Ordinal)
            .ThenBy(x => x.VersionNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.VersionNumber, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Select(x => Map(x, kind == SchemaContractKind.Unspecified ? MapKind(x.ArtifactType) : kind))
            .GroupBy(x => (x.ContractKind, Key: (x.ContractKey ?? string.Empty).ToUpperInvariant(), Version: (x.ContractVersion ?? string.Empty).ToUpperInvariant()))
            .Select(x => x.First())
            .Take(take)
            .ToArray();
    }

    private async Task<IReadOnlyList<KnOwlContractSummary>> Search(string type, string text, int limit, CancellationToken cancellationToken)
    {
        var result = await _catalogClient.SearchAsync(type, text, limit, cancellationToken);
        if (result is null || result.Status != KnOwlContractCatalogStatus.Found)
            throw new InvalidOperationException(result?.Message ?? "KnOwl contract search returned no result.");
        return result.Contracts;
    }

    private SchemaContractCatalogItem Map(KnOwlContractSummary contract, SchemaContractKind kind)
    {
        var key = contract.ContractKey ?? string.Empty;
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

    private static SchemaContractKind MapKind(ContractArtifactType type) => type switch
    {
        ContractArtifactType.Event => SchemaContractKind.Event,
        ContractArtifactType.Command => SchemaContractKind.Command,
        ContractArtifactType.CommandRequest => SchemaContractKind.CommandRequest,
        ContractArtifactType.CommandReply => SchemaContractKind.CommandResponse,
        _ => SchemaContractKind.Unspecified
    };
}
