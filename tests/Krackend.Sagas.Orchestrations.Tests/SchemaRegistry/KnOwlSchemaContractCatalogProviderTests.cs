using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlSchemaContractCatalogProviderTests
{
    [Theory]
    [InlineData(SchemaContractKind.Event, "event")]
    [InlineData(SchemaContractKind.Command, "command")]
    [InlineData(SchemaContractKind.CommandRequest, "command")]
    public async Task Search_DelegatesFilteringAndLimitToKnOwl(SchemaContractKind kind, string type)
    {
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        client.SearchAsync(type, "sales & orders", 50, Arg.Any<CancellationToken>())
            .Returns(Found(Summary(ContractArtifactType.Event, "sales.orders")));
        var provider = CreateProvider(client);
        var result = await provider.SearchAsync(new SchemaContractCatalogSearchRequest
        {
            ContractKind = kind, SearchText = " sales & orders ", Take = 50
        });
        Assert.Equal(kind, Assert.Single(result).ContractKind);
        await client.Received(1).SearchAsync(type, "sales & orders", 50, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAllDeployedAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(500, 100)]
    public async Task Search_NormalizesLimit(int requested, int expected)
    {
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        client.SearchAsync("event", "", expected, Arg.Any<CancellationToken>()).Returns(Found());
        await CreateProvider(client).SearchAsync(new() { ContractKind = SchemaContractKind.Event, Take = requested });
        await client.Received(1).SearchAsync("event", "", expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_CommandResponses_ExcludesLegacyRequestsWithoutReplies()
    {
        var request = Summary(ContractArtifactType.CommandRequest, "legacy.request.only");
        var command = Summary(ContractArtifactType.Command, "modern.command");
        var reply = Summary(ContractArtifactType.CommandReply, "legacy.has.reply");
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        client.SearchAsync("command", "", 100, Arg.Any<CancellationToken>())
            .Returns(Found(command, request with { ArtifactType = ContractArtifactType.Command }));
        client.SearchAsync("request", "", 100, Arg.Any<CancellationToken>()).Returns(Found(request));
        client.SearchAsync("reply", "", 100, Arg.Any<CancellationToken>()).Returns(Found(reply));
        var result = await CreateProvider(client).SearchAsync(new() { ContractKind = SchemaContractKind.CommandResponse });
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.ContractKey == "modern.command");
        Assert.Contains(result, x => x.ContractKey == "legacy.has.reply");
        Assert.DoesNotContain(result, x => x.ContractKey == "legacy.request.only");
        Assert.All(result, x => Assert.Equal(SchemaContractKind.CommandResponse, x.ContractKind));
    }

    [Fact]
    public async Task Search_Unspecified_PreservesKindsAndCombinesBeforeLimit()
    {
        var request = Summary(ContractArtifactType.CommandRequest, "legacy.request") with { CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        var command = Summary(ContractArtifactType.Command, "modern.command") with { CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2) };
        var reply = Summary(ContractArtifactType.CommandReply, "legacy.reply") with { CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3) };
        var evt = Summary(ContractArtifactType.Event, "event") with { CreatedAtUtc = DateTime.UtcNow };
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        client.SearchAsync("command", "", 100, Arg.Any<CancellationToken>()).Returns(Found(request with { ArtifactType = ContractArtifactType.Command }, command));
        client.SearchAsync("request", "", 100, Arg.Any<CancellationToken>()).Returns(Found(request));
        client.SearchAsync("reply", "", 100, Arg.Any<CancellationToken>()).Returns(Found(reply));
        client.SearchAsync("event", "", 100, Arg.Any<CancellationToken>()).Returns(Found(evt));
        var result = await CreateProvider(client).SearchAsync(new() { ContractKind = SchemaContractKind.Unspecified, Take = 3 });
        Assert.Collection(result,
            x => Assert.Equal(SchemaContractKind.Event, x.ContractKind),
            x => Assert.Equal(SchemaContractKind.CommandRequest, x.ContractKind),
            x => Assert.Equal(SchemaContractKind.Command, x.ContractKind));
    }

    [Fact]
    public async Task Search_WhenUnavailable_ReportsErrorInsteadOfReturningAnEmptyCatalog()
    {
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        client.SearchAsync("event", "", 25, Arg.Any<CancellationToken>())
            .Returns(new KnOwlContractSearchResult { Status = KnOwlContractCatalogStatus.Unavailable, Message = "KnOwl 2.4.3 required" });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(client).SearchAsync(new() { ContractKind = SchemaContractKind.Event }));
        Assert.Contains("2.4.3", error.Message);
        await client.DidNotReceive().GetAllDeployedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_Disabled_DoesNotCallKnowl()
    {
        var client = Substitute.For<IKnOwlControlPlaneContractCatalogClient>();
        var provider = new KnOwlSchemaContractCatalogProvider(Options.Create(new KnOwlSchemaRegistryOptions { Enabled = false }), client);
        Assert.Empty(await provider.SearchAsync(new() { ContractKind = SchemaContractKind.Event }));
        await client.DidNotReceiveWithAnyArgs().SearchAsync(default!, default!, default, default);
    }

    private static KnOwlSchemaContractCatalogProvider CreateProvider(IKnOwlControlPlaneContractCatalogClient client)
        => new(Options.Create(new KnOwlSchemaRegistryOptions { BaseUri = new Uri("https://knowl.local") }), client);

    private static KnOwlContractSearchResult Found(params KnOwlContractSummary[] summaries)
        => new() { Status = KnOwlContractCatalogStatus.Found, Contracts = summaries };

    private static KnOwlContractSummary Summary(ContractArtifactType type, string key)
        => new() { Id = Guid.NewGuid(), ArtifactType = type, ContractKey = key, VersionNumber = "1.0.0", HasUsableSchema = true };
}
