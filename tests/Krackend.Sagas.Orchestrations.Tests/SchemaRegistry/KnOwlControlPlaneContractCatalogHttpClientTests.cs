using System.Net;
using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlControlPlaneContractCatalogHttpClientTests
{
    [Fact]
    public async Task GetExactAsync_UsesKnOwlExactDeployedContractRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "artifactType": 1,
              "topic": "inventories.reserve",
              "versionNumber": "1.0.0",
              "payloadSchemaJson": "{\"type\":\"object\"}",
              "contentHash": "hash",
              "sourceStatus": "Deployed"
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.Command, "inventories.reserve", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/Command/inventories.reserve/versions/1.0.0", handler.RequestUri.AbsolutePath);
        Assert.Equal("inventories.reserve", result.Contract.Topic);
    }

    [Fact]
    public async Task GetLatestAsync_UsesKnOwlLatestDeployedContractRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "id": "22222222-2222-2222-2222-222222222222",
              "artifactType": 0,
              "topic": "sales.sale.created",
              "versionNumber": "1.2.0",
              "payloadSchemaJson": "{\"type\":\"object\"}",
              "contentHash": "hash",
              "sourceStatus": "Deployed"
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetLatestAsync(ContractArtifactType.Event, "sales.sale.created");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/Event/sales.sale.created/latest", handler.RequestUri.AbsolutePath);
        Assert.Equal("1.2.0", result.Contract.VersionNumber);
    }

    [Fact]
    public async Task GetExactAsync_WhenCatalogReturnsNotFound_MapsToNotFound()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.Event, "missing.event", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.NotFound, result.Status);
    }

    private static KnOwlControlPlaneContractCatalogHttpClient CreateClient(RecordingHttpMessageHandler handler)
        => new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://knowl-control-plane.local/")
        });
}
