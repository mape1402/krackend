using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Resolution;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlSchemaContractResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenAdapterIsDisabled_ReturnsNotConfigured()
    {
        var resolver = CreateResolver(options => options.Enabled = false);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnabledWithoutBaseUri_ReturnsNotConfigured()
    {
        var resolver = CreateResolver(options => options.Enabled = true);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotConfigured, result.Status);
        Assert.Contains("BaseUri", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_WhenContractKeyIsMissing_ReturnsInvalid()
    {
        var resolver = CreateResolver(options =>
        {
            options.Enabled = true;
            options.BaseUri = new Uri("https://knowl-control-plane.local");
        });

        var result = await resolver.ResolveAsync(CreateRequest(contractKey: " "));

        Assert.Equal(SchemaContractResolutionStatus.Invalid, result.Status);
        Assert.Contains("key", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenContractKindIsUnsupported_ReturnsInvalid()
    {
        var resolver = CreateResolver(options =>
        {
            options.Enabled = true;
            options.BaseUri = new Uri("https://knowl-control-plane.local");
        });

        var result = await resolver.ResolveAsync(CreateRequest(contractKind: (SchemaContractKind)999));

        Assert.Equal(SchemaContractResolutionStatus.Invalid, result.Status);
        Assert.Contains("not supported", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenExactEventContractIsDeployed_ReturnsSnapshot()
    {
        var contractId = Guid.NewGuid();
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetExactAsync(ContractArtifactType.Event, "sales.sale.created", "1.0.0", Arg.Any<CancellationToken>())
            .Returns(KnOwlContractCatalogResult.Found(new ContractArtifact
            {
                Id = contractId,
                ArtifactType = ContractArtifactType.Event,
                Topic = "sales.sale.created",
                VersionNumber = "1.0.0",
                PayloadSchemaJson = "{\"type\":\"object\"}",
                ContentHash = "schema-hash",
                SourceStatus = "Deployed"
            }));
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Snapshot);
        Assert.Equal("knowl", result.Snapshot.Reference.ProviderKey);
        Assert.Equal(contractId.ToString(), result.Snapshot.Reference.ContractId);
        Assert.Equal("sales.sale.created", result.Snapshot.Reference.ContractKey);
        Assert.Equal("1.0.0", result.Snapshot.Reference.ContractVersion);
        Assert.Equal(SchemaContractKind.Event, result.Snapshot.Reference.Kind);
        Assert.Equal("ButterMorph", result.Snapshot.SchemaFormat);
        Assert.Equal("{\"type\":\"object\"}", result.Snapshot.SchemaJson);
        Assert.Equal("schema-hash", result.Snapshot.ContentHash);
        Assert.Equal(contractId.ToString(), result.Snapshot.SourceArtifactId);
        Assert.Equal("knowl", result.Snapshot.ResolvedBy);
    }

    [Fact]
    public async Task ResolveAsync_WhenCommandResponseIsRequested_UsesKnOwlCommandArtifactType()
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetExactAsync(ContractArtifactType.Command, "inventories.reserve.completed", "1.0.0", Arg.Any<CancellationToken>())
            .Returns(KnOwlContractCatalogResult.Found(new ContractArtifact
            {
                Id = Guid.NewGuid(),
                ArtifactType = ContractArtifactType.Command,
                Topic = "inventories.reserve.completed",
                VersionNumber = "1.0.0",
                PayloadSchemaJson = "{\"type\":\"object\"}",
                ContentHash = "response-schema-hash",
                SourceStatus = "Deployed"
            }));
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await resolver.ResolveAsync(CreateRequest(
            "inventories.reserve.completed",
            "1.0.0",
            SchemaContractKind.CommandResponse));

        Assert.Equal(SchemaContractResolutionStatus.Resolved, result.Status);
        Assert.Equal(SchemaContractKind.CommandResponse, result.Snapshot.Reference.Kind);
        await catalog.Received(1).GetExactAsync(ContractArtifactType.Command, "inventories.reserve.completed", "1.0.0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenVersionIsMissingAndLatestResolutionIsDisabled_ReturnsInvalid()
    {
        var resolver = CreateResolver(options =>
        {
            options.Enabled = true;
            options.BaseUri = new Uri("https://knowl-control-plane.local");
        });

        var result = await resolver.ResolveAsync(CreateRequest(contractVersion: string.Empty));

        Assert.Equal(SchemaContractResolutionStatus.Invalid, result.Status);
        Assert.Contains("version", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenCatalogReturnsNoResult_ReturnsUnavailable()
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetExactAsync(ContractArtifactType.Event, "sales.sale.created", "1.0.0", Arg.Any<CancellationToken>())
            .Returns((KnOwlContractCatalogResult)null!);
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.Unavailable, result.Status);
        Assert.Contains("no result", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(KnOwlContractCatalogStatus.NotFound, SchemaContractResolutionStatus.NotFound)]
    [InlineData(KnOwlContractCatalogStatus.Invalid, SchemaContractResolutionStatus.Invalid)]
    [InlineData(KnOwlContractCatalogStatus.Unavailable, SchemaContractResolutionStatus.Unavailable)]
    public async Task ResolveAsync_WhenCatalogDoesNotFindContract_MapsCatalogStatus(
        KnOwlContractCatalogStatus catalogStatus,
        SchemaContractResolutionStatus expectedStatus)
    {
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            KnOwlContractCatalogResult.Failed(catalogStatus, "catalog message"));

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("catalog message", result.Message);
    }

    [Fact]
    public async Task ResolveAsync_WhenCatalogFoundButContractIsNull_ReturnsNotFound()
    {
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            KnOwlContractCatalogResult.Found(null!));

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.NotFound, result.Status);
        Assert.Contains("no contract", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenLatestResolutionIsEnabled_UsesLatestEndpoint()
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetLatestAsync(ContractArtifactType.Event, "sales.sale.created", Arg.Any<CancellationToken>())
            .Returns(KnOwlContractCatalogResult.Found(new ContractArtifact
            {
                Id = Guid.NewGuid(),
                ArtifactType = ContractArtifactType.Event,
                Topic = "sales.sale.created",
                VersionNumber = "1.2.0",
                PayloadSchemaJson = "{\"type\":\"object\"}",
                ContentHash = "latest-schema-hash",
                SourceStatus = "Deployed"
            }));
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
                options.AllowLatestVersionResolution = true;
            },
            catalog);

        var result = await resolver.ResolveAsync(CreateRequest(contractVersion: string.Empty));

        Assert.Equal(SchemaContractResolutionStatus.Resolved, result.Status);
        Assert.Equal("1.2.0", result.Snapshot.Reference.ContractVersion);
        await catalog.Received(1).GetLatestAsync(ContractArtifactType.Event, "sales.sale.created", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenContractIsNotDeployed_ReturnsInvalid()
    {
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            KnOwlContractCatalogResult.Found(new ContractArtifact
            {
                Id = Guid.NewGuid(),
                ArtifactType = ContractArtifactType.Event,
                Topic = "sales.sale.created",
                VersionNumber = "1.0.0",
                PayloadSchemaJson = "{\"type\":\"object\"}",
                ContentHash = "schema-hash",
                SourceStatus = "Draft"
            }));

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.Invalid, result.Status);
        Assert.Contains("not deployed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WhenContractDoesNotContainPayloadSchema_ReturnsInvalid()
    {
        var resolver = CreateResolver(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            KnOwlContractCatalogResult.Found(new ContractArtifact
            {
                Id = Guid.NewGuid(),
                ArtifactType = ContractArtifactType.Event,
                Topic = "sales.sale.created",
                VersionNumber = "1.0.0",
                PayloadSchemaJson = string.Empty,
                ContentHash = "schema-hash",
                SourceStatus = "Deployed"
            }));

        var result = await resolver.ResolveAsync(CreateRequest());

        Assert.Equal(SchemaContractResolutionStatus.Invalid, result.Status);
        Assert.Contains("payload schema", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static KnOwlSchemaContractResolver CreateResolver(Action<KnOwlSchemaRegistryOptions> configure)
        => CreateResolver(configure, KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.NotFound, "Not found."));

    private static KnOwlSchemaContractResolver CreateResolver(
        Action<KnOwlSchemaRegistryOptions> configure,
        KnOwlContractCatalogResult result)
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetExactAsync(Arg.Any<ContractArtifactType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);

        return CreateResolver(configure, catalog);
    }

    private static KnOwlSchemaContractResolver CreateResolver(
        Action<KnOwlSchemaRegistryOptions> configure,
        IKnOwlControlPlaneContractCatalogClient catalog)
    {
        var options = new KnOwlSchemaRegistryOptions();
        configure(options);

        return new KnOwlSchemaContractResolver(Options.Create(options), catalog);
    }

    private static SchemaContractResolutionRequest CreateRequest(
        string contractKey = "sales.sale.created",
        string contractVersion = "1.0.0",
        SchemaContractKind contractKind = SchemaContractKind.Event)
        => new()
        {
            Reference = new SchemaContractReference
            {
                ProviderKey = "knowl",
                ContractKey = contractKey,
                ContractVersion = contractVersion,
                Kind = contractKind
            },
            RequireRemoteResolution = true
        };
}
