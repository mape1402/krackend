using System.Net;
using KnOwl.Contracts.Artifacts;
using Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

namespace Krackend.Sagas.Orchestrations.Tests.SchemaRegistry;

public sealed class KnOwlControlPlaneContractCatalogHttpClientTests
{
    [Fact]
    public async Task GetAllDeployedAsync_UsesKnOwlCatalogRouteAndDeserializesContracts()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            [
              {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 0,
                "topic": "sales.sale.created",
                "versionNumber": "1.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "event-hash",
                "sourceStatus": "Deployed"
              },
              {
                "id": "22222222-2222-2222-2222-222222222222",
                "artifactType": 1,
                "topic": "inventories.reserve",
                "versionNumber": "1.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "command-hash",
                "sourceStatus": "Deployed"
              }
            ]
            """));
        var client = CreateClient(handler);

        var result = await client.GetAllDeployedAsync();

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/artifacts", handler.RequestUri.AbsolutePath);
        Assert.Equal(2, result.Contracts.Count);
        Assert.Contains(result.Contracts, contract => contract.Topic == "sales.sale.created");
        Assert.Contains(result.Contracts, contract => contract.Topic == "inventories.reserve");
    }

    [Fact]
    public async Task GetExactAsync_WhenCommandRequestIsRequested_UsesKnOwlCommandRouteAndReturnsRequestArtifact()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "commandKey": "inventories.reserve",
              "version": "1.0.0",
              "requestArtifact": {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 1,
                "topic": "inventories.reserve",
                "versionNumber": "1.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "request-hash",
                "sourceStatus": "Deployed"
              },
              "replyArtifact": {
                "id": "22222222-2222-2222-2222-222222222222",
                "artifactType": 2,
                "topic": "inventories.reserve",
                "versionNumber": "1.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "reply-hash",
                "sourceStatus": "Deployed"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.CommandRequest, "inventories.reserve", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/commands/inventories.reserve/versions/1.0.0", handler.RequestUri.AbsolutePath);
        Assert.Equal("inventories.reserve", result.Contract.Topic);
        Assert.Equal(ContractArtifactType.CommandRequest, result.Contract.ArtifactType);
        Assert.Equal("request-hash", result.Contract.ContentHash);
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
        Assert.Equal("/contracts/events/sales.sale.created/latest", handler.RequestUri.AbsolutePath);
        Assert.Equal("1.2.0", result.Contract.VersionNumber);
    }

    [Fact]
    public async Task GetExactCommandAsync_DeserializesCommandRequestAndReplyArtifacts()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "commandKey": "payments.capture",
              "version": "2.0.0",
              "requestArtifact": {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 1,
                "topic": "payments.capture",
                "versionNumber": "2.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "request-hash",
                "sourceStatus": "Deployed"
              },
              "replyArtifact": {
                "id": "22222222-2222-2222-2222-222222222222",
                "artifactType": 2,
                "topic": "payments.capture",
                "versionNumber": "2.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "reply-hash",
                "sourceStatus": "Deployed"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetExactCommandAsync("payments.capture", "2.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/commands/payments.capture/versions/2.0.0", handler.RequestUri.AbsolutePath);
        Assert.NotNull(result.Command);
        var command = result.Command!;
        Assert.Equal("payments.capture", command.CommandKey);
        Assert.Equal("2.0.0", command.Version);
        Assert.Equal("request-hash", command.RequestArtifact.ContentHash);
        Assert.NotNull(command.ReplyArtifact);
        Assert.Equal("reply-hash", command.ReplyArtifact!.ContentHash);
    }

    [Fact]
    public async Task GetLatestAsync_WhenCommandReplyIsRequested_UsesKnOwlCommandRouteAndReturnsReplyArtifact()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "commandKey": "payments.capture",
              "version": "2.1.0",
              "requestArtifact": {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 1,
                "topic": "payments.capture",
                "versionNumber": "2.1.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "request-hash",
                "sourceStatus": "Deployed"
              },
              "replyArtifact": {
                "id": "22222222-2222-2222-2222-222222222222",
                "artifactType": 2,
                "topic": "payments.capture",
                "versionNumber": "2.1.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "reply-hash",
                "sourceStatus": "Deployed"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetLatestAsync(ContractArtifactType.CommandReply, "payments.capture");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/commands/payments.capture/latest", handler.RequestUri.AbsolutePath);
        Assert.Equal(ContractArtifactType.CommandReply, result.Contract.ArtifactType);
        Assert.Equal("reply-hash", result.Contract.ContentHash);
    }

    [Fact]
    public async Task GetLatestCommandAsync_UsesKnOwlLatestCommandRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "commandKey": "shipments.dispatch",
              "version": "3.0.0",
              "requestArtifact": {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 1,
                "topic": "shipments.dispatch",
                "versionNumber": "3.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "request-hash",
                "sourceStatus": "Deployed"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetLatestCommandAsync("shipments.dispatch");

        Assert.Equal(KnOwlContractCatalogStatus.Found, result.Status);
        Assert.Equal("/contracts/commands/shipments.dispatch/latest", handler.RequestUri.AbsolutePath);
        Assert.Equal("shipments.dispatch", result.Command.CommandKey);
        Assert.Equal("3.0.0", result.Command.Version);
    }

    [Fact]
    public async Task GetExactAsync_WhenCommandReplyIsMissing_ReturnsNotFound()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
            """
            {
              "commandKey": "shipments.dispatch",
              "version": "3.0.0",
              "requestArtifact": {
                "id": "11111111-1111-1111-1111-111111111111",
                "artifactType": 1,
                "topic": "shipments.dispatch",
                "versionNumber": "3.0.0",
                "payloadSchemaJson": "{\"type\":\"object\"}",
                "contentHash": "request-hash",
                "sourceStatus": "Deployed"
              }
            }
            """));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.CommandReply, "shipments.dispatch", "3.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.NotFound, result.Status);
        Assert.Contains("CommandReply", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetExactAsync_WhenCommandRouteReturnsNotFound_MapsToNotFound()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.CommandRequest, "missing.command", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.NotFound, result.Status);
        Assert.Equal("/contracts/commands/missing.command/versions/1.0.0", handler.RequestUri.AbsolutePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_WhenArtifactTypeIsUnsupported_ReturnsInvalid(bool latest)
    {
        var client = CreateClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}")));

        var result = latest
            ? await client.GetLatestAsync((ContractArtifactType)999, "ignored")
            : await client.GetExactAsync((ContractArtifactType)999, "ignored", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Invalid, result.Status);
        Assert.Contains("does not support", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExactCommandAsync_WhenCommandBodyIsNull_ReturnsNotFound()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("null"));
        var client = CreateClient(handler);

        var result = await client.GetExactCommandAsync("empty.command", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.NotFound, result.Status);
        Assert.Contains("no command", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExactCommandAsync_WhenCatalogReturnsServerError_MapsToUnavailable()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var result = await client.GetExactCommandAsync("payments.capture", "2.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, result.Status);
        Assert.Contains("HTTP 500", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetExactCommandAsync_WhenTransportFails_MapsToUnavailable()
    {
        var timeout = CreateClient(new RecordingHttpMessageHandler(_ => throw new OperationCanceledException()));
        var transportError = CreateClient(new RecordingHttpMessageHandler(_ => throw new HttpRequestException("network down")));
        var invalidBaseAddress = new KnOwlControlPlaneContractCatalogHttpClient(
            new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}"))));

        var timeoutResult = await timeout.GetExactCommandAsync("payments.capture", "2.0.0");
        var transportResult = await transportError.GetExactCommandAsync("payments.capture", "2.0.0");
        var invalidResult = await invalidBaseAddress.GetExactCommandAsync("payments.capture", "2.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, timeoutResult.Status);
        Assert.Contains("timed out", timeoutResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, transportResult.Status);
        Assert.Contains("network down", transportResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, invalidResult.Status);
    }

    [Fact]
    public void FailedCommandResult_WhenMessageIsNull_UsesEmptyMessage()
    {
        var result = KnOwlCommandContractCatalogResult.Failed(KnOwlContractCatalogStatus.Invalid, null!);

        Assert.Equal(KnOwlContractCatalogStatus.Invalid, result.Status);
        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public async Task GetExactAsync_WhenCatalogReturnsNotFound_MapsToNotFound()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.GetExactAsync(ContractArtifactType.Event, "missing.event", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetAllDeployedAsync_WhenCatalogReturnsServerError_MapsToUnavailable()
    {
        var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        var result = await client.GetAllDeployedAsync();

        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, result.Status);
        Assert.Contains("HTTP 500", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAllDeployedAsync_WhenTransportFails_MapsToUnavailable()
    {
        var timeout = CreateClient(new RecordingHttpMessageHandler(_ => throw new OperationCanceledException()));
        var transportError = CreateClient(new RecordingHttpMessageHandler(_ => throw new HttpRequestException("network down")));
        var invalidBaseAddress = new KnOwlControlPlaneContractCatalogHttpClient(
            new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("[]"))));

        var timeoutResult = await timeout.GetAllDeployedAsync();
        var transportResult = await transportError.GetAllDeployedAsync();
        var invalidResult = await invalidBaseAddress.GetAllDeployedAsync();

        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, timeoutResult.Status);
        Assert.Contains("timed out", timeoutResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, transportResult.Status);
        Assert.Contains("network down", transportResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, invalidResult.Status);
    }

    [Fact]
    public async Task GetExactAsync_WhenTransportFails_MapsToUnavailable()
    {
        var timeout = CreateClient(new RecordingHttpMessageHandler(_ => throw new OperationCanceledException()));
        var transportError = CreateClient(new RecordingHttpMessageHandler(_ => throw new HttpRequestException("network down")));
        var invalidBaseAddress = new KnOwlControlPlaneContractCatalogHttpClient(
            new HttpClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json("{}"))));

        var timeoutResult = await timeout.GetExactAsync(ContractArtifactType.Event, "sales.sale.created", "1.0.0");
        var transportResult = await transportError.GetExactAsync(ContractArtifactType.Event, "sales.sale.created", "1.0.0");
        var invalidResult = await invalidBaseAddress.GetExactAsync(ContractArtifactType.Event, "sales.sale.created", "1.0.0");

        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, timeoutResult.Status);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, transportResult.Status);
        Assert.Equal(KnOwlContractCatalogStatus.Unavailable, invalidResult.Status);
    }

    private static KnOwlControlPlaneContractCatalogHttpClient CreateClient(RecordingHttpMessageHandler handler)
        => new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://knowl-control-plane.local/")
        });
}
