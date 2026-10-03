using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlSchemaContractCatalogProviderTests
{
    [Fact]
    public async Task SearchAsync_WhenProviderIsDisabled_ReturnsEmptyWithoutCallingCatalog()
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        var provider = CreateProvider(options => options.Enabled = false, catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Event
        });

        Assert.Empty(result);
        await catalog.DidNotReceive().GetAllDeployedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ReturnsDeployedEventContractsMatchingSearchText()
    {
        var eventId = Guid.NewGuid();
        var catalog = CreateCatalogClient(
            CreateArtifact(eventId, ContractArtifactType.Event, "events.sales.sale.created", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Event, "events.inventory.changed", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Event, "events.sales.sale.created", "0.9.0", sourceStatus: "Draft"));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
                options.ProviderKey = "knowl-control";
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Event,
            SearchText = "sale",
            Take = 10
        });

        var item = Assert.Single(result);
        Assert.Equal("knowl-control", item.ProviderKey);
        Assert.Equal(eventId.ToString(), item.ContractId);
        Assert.Equal("events.sales.sale.created", item.ContractKey);
        Assert.Equal("1.0.0", item.ContractVersion);
        Assert.Equal(SchemaContractKind.Event, item.ContractKind);
        Assert.Equal("events.sales.sale.created v1.0.0", item.DisplayName);
    }

    [Fact]
    public async Task SearchAsync_WhenCommandCatalogIsRequested_ReturnsCommandRequestAsCommand()
    {
        var catalog = CreateCatalogClient(
            CreateArtifact(
                Guid.NewGuid(),
                ContractArtifactType.CommandRequest,
                "inventories.reserve",
                "1.1.0",
                contentHash: "request-11",
                createdAtUtc: DateTime.UtcNow.AddMinutes(-1)),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.CommandReply, "inventories.reserve", "1.1.0", contentHash: "reply-11"),
            CreateArtifact(
                Guid.NewGuid(),
                ContractArtifactType.CommandRequest,
                "inventories.reserve",
                "1.0.0",
                contentHash: "request-10",
                createdAtUtc: DateTime.UtcNow.AddMinutes(-2)));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Command,
            SearchText = "inventories",
            Take = 10
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(SchemaContractKind.Command, item.ContractKind));
        Assert.All(result, item => Assert.StartsWith("request-", item.ContentHash, StringComparison.Ordinal));
        Assert.Equal("1.1.0", result.First().ContractVersion);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestContractsFirst()
    {
        var oldest = CreateArtifact(
            Guid.NewGuid(),
            ContractArtifactType.Event,
            "events.sales.sale.created",
            "1.0.0",
            createdAtUtc: new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        var newest = CreateArtifact(
            Guid.NewGuid(),
            ContractArtifactType.Event,
            "events.sales.sale.created",
            "1.0.1",
            createdAtUtc: new DateTime(2026, 8, 20, 11, 0, 0, DateTimeKind.Utc));
        var catalog = CreateCatalogClient(oldest, newest);
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Event,
            SearchText = "sale",
            Take = 10
        });

        Assert.Collection(
            result,
            item => Assert.Equal("1.0.1", item.ContractVersion),
            item => Assert.Equal("1.0.0", item.ContractVersion));
    }

    [Fact]
    public async Task SearchAsync_WhenKnOwlReturnsCompoundCommandArtifact_ExposesItForCommandInputs()
    {
        var catalog = CreateCatalogClient(
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Command, "commands.sales.sale.create", "1.0.0", contentHash: "compound-command"));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var commandResult = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Command,
            SearchText = "sales"
        });
        var requestResult = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.CommandRequest,
            SearchText = "sales"
        });

        var commandItem = Assert.Single(commandResult);
        var requestItem = Assert.Single(requestResult);
        Assert.Equal(SchemaContractKind.Command, commandItem.ContractKind);
        Assert.Equal(SchemaContractKind.CommandRequest, requestItem.ContractKind);
        Assert.Equal("commands.sales.sale.create", commandItem.ContractKey);
        Assert.Equal("commands.sales.sale.create", requestItem.ContractKey);
    }

    [Fact]
    public async Task SearchAsync_WhenKindIsUnspecified_MapsAllDeployedArtifactKinds()
    {
        var catalog = CreateCatalogClient(
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Event, "events.sales.sale.created", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.CommandRequest, "commands.inventory.reserve.request", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.CommandReply, "commands.inventory.reserve.reply", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Command, "commands.sales.sale.create", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), (ContractArtifactType)999, "contracts.unknown", ""));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
                options.ProviderKey = " ";
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Unspecified,
            SearchText = " ",
            Take = 0
        });

        Assert.Equal(5, result.Count);
        Assert.All(result, item => Assert.Equal("knowl", item.ProviderKey));
        Assert.Contains(result, item => item.ContractKind == SchemaContractKind.Event);
        Assert.Contains(result, item => item.ContractKind == SchemaContractKind.CommandRequest);
        Assert.Contains(result, item => item.ContractKind == SchemaContractKind.CommandResponse);
        Assert.Contains(result, item => item.ContractKind == SchemaContractKind.Command);
        Assert.Contains(result, item =>
            item.ContractKind == SchemaContractKind.Unspecified &&
            item.ContractKey == "contracts.unknown" &&
            item.DisplayName == "contracts.unknown");
    }

    [Fact]
    public async Task SearchAsync_WhenCommandResponseIsRequested_ReturnsRepliesAndCompoundCommands()
    {
        var catalog = CreateCatalogClient(
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.CommandReply, "commands.inventory.reserve", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.Command, "commands.sales.sale.create", "1.0.0"),
            CreateArtifact(Guid.NewGuid(), ContractArtifactType.CommandRequest, "commands.inventory.reserve", "1.0.0"));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.CommandResponse,
            SearchText = "commands"
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(SchemaContractKind.CommandResponse, item.ContractKind));
        Assert.Contains(result, item => item.ContractKey == "commands.inventory.reserve");
        Assert.Contains(result, item => item.ContractKey == "commands.sales.sale.create");
    }

    [Fact]
    public async Task SearchAsync_WhenCatalogIsUnavailable_ReturnsEmpty()
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetAllDeployedAsync(Arg.Any<CancellationToken>())
            .Returns(KnOwlContractCatalogResult.Failed(KnOwlContractCatalogStatus.Unavailable, "down"));
        var provider = CreateProvider(
            options =>
            {
                options.Enabled = true;
                options.BaseUri = new Uri("https://knowl-control-plane.local");
            },
            catalog);

        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = SchemaContractKind.Event
        });

        Assert.Empty(result);
    }

    private static KnOwlSchemaContractCatalogProvider CreateProvider(
        Action<KnOwlSchemaRegistryOptions> configure,
        IKnOwlControlPlaneContractCatalogClient catalog)
    {
        var options = new KnOwlSchemaRegistryOptions();
        configure(options);
        return new KnOwlSchemaContractCatalogProvider(Options.Create(options), catalog);
    }

    private static IKnOwlControlPlaneContractCatalogClient CreateCatalogClient(params ContractArtifact[] artifacts)
    {
        var catalog = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        catalog.GetAllDeployedAsync(Arg.Any<CancellationToken>())
            .Returns(KnOwlContractCatalogResult.FoundMany(artifacts));
        return catalog;
    }

    private static ContractArtifact CreateArtifact(
        Guid id,
        ContractArtifactType artifactType,
        string topic,
        string version,
        string sourceStatus = "Deployed",
        string contentHash = "schema-hash",
        DateTime? createdAtUtc = null)
        => new()
        {
            Id = id,
            ArtifactType = artifactType,
            Topic = topic,
            VersionNumber = version,
            PayloadSchemaJson = "{\"type\":\"object\"}",
            ContentHash = contentHash,
            SourceStatus = sourceStatus,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
}
