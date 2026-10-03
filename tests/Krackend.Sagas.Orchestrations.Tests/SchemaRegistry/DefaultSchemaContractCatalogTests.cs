namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

public sealed class DefaultSchemaContractCatalogTests
{
    [Fact]
    public void ConstructorRejectsNullProviders()
        => Assert.Throws<ArgumentNullException>(() => new DefaultSchemaContractCatalog(null!));

    [Fact]
    public async Task SearchReturnsEmptyWhenNoProviderMatches()
    {
        var provider = new RecordingCatalogProvider("knowl", [Item("knowl", "orders.created", "1.0.0")]);
        var catalog = new DefaultSchemaContractCatalog([provider]);

        var result = await catalog.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ProviderKey = "missing"
        });

        Assert.Empty(result);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task SearchNormalizesTakeQueriesProvidersAndDeduplicates()
    {
        var duplicate = Item("knowl", "orders.created", "1.0.0");
        var first = new RecordingCatalogProvider("knowl", [duplicate, duplicate]);
        var second = new RecordingCatalogProvider("avro", [Item("avro", "orders.created", "1.0.0")]);
        var catalog = new DefaultSchemaContractCatalog([first, second]);

        var result = await catalog.SearchAsync(null!);

        Assert.Equal(2, result.Count);
        Assert.All(first.Requests, request => Assert.Equal(25, request.Take));
        Assert.All(second.Requests, request => Assert.Equal(23, request.Take));
        Assert.Contains(result, item => item.ProviderKey == "knowl");
        Assert.Contains(result, item => item.ProviderKey == "avro");
    }

    [Fact]
    public async Task SearchFiltersProvidersCaseInsensitiveAndTrimsKey()
    {
        var knowl = new RecordingCatalogProvider("knowl", [Item("knowl", "orders.created", "1.0.0")]);
        var avro = new RecordingCatalogProvider("avro", [Item("avro", "orders.created", "1.0.0")]);
        var catalog = new DefaultSchemaContractCatalog([knowl, avro]);

        var result = await catalog.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ProviderKey = " KNOWL ",
            Take = 1
        });

        Assert.Equal("knowl", Assert.Single(result).ProviderKey);
        Assert.Single(knowl.Requests);
        Assert.Empty(avro.Requests);
        Assert.Equal(1, knowl.Requests[0].Take);
    }

    [Fact]
    public async Task SearchCapsTakeAndStopsWhenRawResultsReachLimit()
    {
        var fullProvider = new RecordingCatalogProvider(
            "knowl",
            Enumerable.Range(0, 100)
                .Select(index => Item("knowl", $"orders.{index}", $"1.0.{index}"))
                .ToArray());
        var skippedProvider = new RecordingCatalogProvider("avro", [Item("avro", "orders.created", "1.0.0")]);
        var catalog = new DefaultSchemaContractCatalog([fullProvider, skippedProvider]);

        var result = await catalog.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            Take = 500
        });

        Assert.Equal(100, result.Count);
        Assert.Single(fullProvider.Requests);
        Assert.Equal(100, fullProvider.Requests[0].Take);
        Assert.Empty(skippedProvider.Requests);
    }

    [Fact]
    public async Task SearchContinuesWhenProviderReturnsNullResults()
    {
        var emptyProvider = new RecordingCatalogProvider("empty", null);
        var provider = new RecordingCatalogProvider("knowl", [Item("knowl", "orders.created", "1.0.0")]);
        var catalog = new DefaultSchemaContractCatalog([emptyProvider, provider]);

        var result = await catalog.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            Take = 2
        });

        Assert.Equal("knowl", Assert.Single(result).ProviderKey);
        Assert.Single(emptyProvider.Requests);
        Assert.Single(provider.Requests);
        Assert.Equal(2, provider.Requests[0].Take);
    }

    private static SchemaContractCatalogItem Item(
        string providerKey,
        string contractKey,
        string version)
        => new()
        {
            ProviderKey = providerKey,
            ContractId = $"{providerKey}:{contractKey}:{version}",
            ContractKey = contractKey,
            ContractVersion = version,
            ContractKind = SchemaContractKind.Event,
            ContentHash = $"{contractKey}:{version}:hash",
            DisplayName = $"{contractKey} v{version}"
        };

    private sealed class RecordingCatalogProvider : ISchemaContractCatalogProvider
    {
        private readonly IReadOnlyCollection<SchemaContractCatalogItem>? _results;

        public RecordingCatalogProvider(
            string providerKey,
            IReadOnlyCollection<SchemaContractCatalogItem>? results)
        {
            ProviderKey = providerKey;
            _results = results;
        }

        public string ProviderKey { get; }

        public List<SchemaContractCatalogSearchRequest> Requests { get; } = [];

        public Task<IReadOnlyCollection<SchemaContractCatalogItem>> SearchAsync(
            SchemaContractCatalogSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_results!);
        }
    }
}
