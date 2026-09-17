using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class SchemaRegistryResolutionTests
{
    [Fact]
    public void SchemaRegistryContractsExposeResolutionStateAndExceptions()
    {
        var snapshot = new SchemaContractSnapshot
        {
            Reference = new SchemaContractReference
            {
                ProviderKey = "knowl",
                Kind = SchemaContractKind.CommandResponse,
                ContractKey = "inventories.reserve",
                ContractVersion = "1.0.0"
            },
            SchemaFormat = "ButterMorph",
            SchemaJson = """{"type":"object"}""",
            ContentHash = "hash-1",
            SourceArtifactId = "artifact-1",
            ResolvedBy = "knowl-control-plane",
            ResolvedAtUtc = DateTimeOffset.UtcNow
        };
        var request = new SchemaContractResolutionRequest
        {
            Reference = snapshot.Reference,
            RequireRemoteResolution = true
        };
        var resolved = SchemaContractResolutionResult.Resolved(snapshot);
        var failed = SchemaContractResolutionResult.Failed(SchemaContractResolutionStatus.NotFound, "missing");
        var inner = new InvalidOperationException("registry failed");
        var simpleException = new SchemaRegistryException("schema failed");
        var nestedException = new SchemaRegistryException("schema failed", inner);

        Assert.True(request.RequireRemoteResolution);
        Assert.Same(snapshot, resolved.Snapshot);
        Assert.Equal(SchemaContractResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("missing", failed.Message);
        Assert.Equal("schema failed", simpleException.Message);
        Assert.Same(inner, nestedException.InnerException);
    }

    [Fact]
    public async Task InMemorySnapshotStoreResolvesSnapshotsByCaseInsensitiveReference()
    {
        var store = new InMemorySchemaContractSnapshotStore();
        var snapshot = new SchemaContractSnapshot
        {
            Reference = new SchemaContractReference
            {
                ProviderKey = "KnOwl",
                Kind = SchemaContractKind.Event,
                ContractKey = "Sales.Sale.Created",
                ContractVersion = "1.0.0"
            },
            ContentHash = "hash-1"
        };

        store.Set(snapshot);

        var resolved = await store.GetAsync(new SchemaContractReference
        {
            ProviderKey = "knowl",
            Kind = SchemaContractKind.Event,
            ContractKey = "sales.sale.created",
            ContractVersion = "1.0.0"
        });
        var missing = await store.GetAsync(new SchemaContractReference
        {
            ProviderKey = "knowl",
            Kind = SchemaContractKind.CommandRequest,
            ContractKey = "sales.sale.created",
            ContractVersion = "1.0.0"
        });

        Assert.Same(snapshot, resolved);
        Assert.Null(missing);
        Assert.Throws<ArgumentNullException>(() => store.Set(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetAsync(null!));
    }

    [Fact]
    public async Task ResolverCatalogSelectsConfiguredResolverOrFallsBackToNoop()
    {
        var services = new ServiceCollection();
        var knowl = new RecordingResolver("knowl");
        var legacy = new RecordingResolver("legacy-provider-id");
        services.AddSingleton<ISchemaContractResolver>(knowl);
        services.AddSingleton<ISchemaContractResolver>(legacy);
        var catalog = new DefaultSchemaContractResolverCatalog(services.BuildServiceProvider());

        var byKey = catalog.GetResolver(new SchemaContractReference { ProviderKey = "KNOWL", ContractKey = "sales.sale.created" });
        var byId = catalog.GetResolver(new SchemaContractReference { ProviderId = "legacy-provider-id", ContractKey = "legacy.event" });
        var noop = catalog.GetResolver(new SchemaContractReference { ProviderKey = "missing", ContractKey = "missing.event" });

        Assert.Same(knowl, byKey);
        Assert.Same(legacy, byId);
        Assert.Equal("noop", noop.ProviderKey);
        var result = await noop.ResolveAsync(new SchemaContractResolutionRequest
        {
            Reference = new SchemaContractReference { ContractKey = "missing.event" }
        });
        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("missing.event", result.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => new DefaultSchemaContractResolverCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.GetResolver(null!));
    }

    private sealed class RecordingResolver : ISchemaContractResolver
    {
        public RecordingResolver(string providerKey)
        {
            ProviderKey = providerKey;
        }

        public string ProviderKey { get; }

        public Task<SchemaContractResolutionResult> ResolveAsync(
            SchemaContractResolutionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotFound,
                "not found"));
    }
}
