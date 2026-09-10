namespace Krackend.Sagas.Orchestrations.Tests.Client;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;

public sealed class OrchestrationOperationClientTests
{
    [Fact]
    public async Task ReportSuccessAsync_WhenContextWasClosed_PublishesExecutionMetadata()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{}"
            }
        });

        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        client.Begin(typeof(string));
        client.Close();

        await client.ReportSuccessAsync(
            typeof(string),
            typeof(int),
            new { ok = true },
            new OrchestrationOperationOptions
            {
                ServiceName = "inventories-api",
                OperationName = "inventories.reserve",
                Metadata =
                {
                    ["node"] = JsonValue.Create("local")
                }
            });

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.True(publisher.ResultMetadata.Succeeded);
        Assert.Equal(typeof(string).FullName, publisher.ResultMetadata.RequestType);
        Assert.Equal(typeof(int).FullName, publisher.ResultMetadata.ResponseType);
        Assert.Equal("inventories-api", publisher.ResultMetadata.ServiceName);
        Assert.Equal("inventories.reserve", publisher.ResultMetadata.OperationName);
        Assert.Equal("local", publisher.ResultMetadata.Metadata["node"]?.GetValue<string>());
        Assert.NotNull(publisher.Payload);
    }

    [Fact]
    public async Task GenericReportFailureAsyncPublishesMappedErrorMetadata()
    {
        var services = new ServiceCollection();
        services
            .AddKrackendOrchestrationsClient()
            .MapException<InvalidOperationException>("BusinessRuleFailed", isRetryableCandidate: false);
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{}"
            }
        });

        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        client.Begin<string>();

        await client.ReportFailureAsync<string>(
            new InvalidOperationException("No inventory"),
            new OrchestrationOperationOptions
            {
                ServiceName = "inventories-api",
                OperationName = "inventories.reserve"
            });

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.False(publisher.ResultMetadata.Succeeded);
        Assert.Equal("BusinessRuleFailed", publisher.ResultMetadata.ErrorCode);
        Assert.False(publisher.ResultMetadata.IsRetryableCandidate);
        Assert.Equal("inventories-api", publisher.ResultMetadata.ServiceName);
        Assert.Equal("inventories.reserve", publisher.ResultMetadata.OperationName);
        Assert.Null(publisher.Payload);
    }

    [Fact]
    public async Task ReportSuccessAsyncPublishesBusinessPayloadWithoutWrappingExecutionMetadata()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{}"
            }
        });

        var businessPayload = JsonNode.Parse(
            """
            {
              "reservationId": "reservation-1",
              "saleId": "sale-1"
            }
            """);

        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        client.Begin<ReserveInventoryRequest>();

        await client.ReportSuccessAsync(
            typeof(ReserveInventoryRequest),
            typeof(ReserveInventoryResponse),
            businessPayload,
            new OrchestrationOperationOptions
            {
                ServiceName = "inventories-api",
                OperationName = "inventories.reserve"
            });

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        var publishedPayload = Assert.IsAssignableFrom<JsonNode>(publisher.Payload);
        Assert.NotNull(publisher.ResultMetadata);
        Assert.Equal("reservation-1", publishedPayload["reservationId"]?.GetValue<string>());
        Assert.Equal("sale-1", publishedPayload["saleId"]?.GetValue<string>());
        Assert.Null(publishedPayload["Succeeded"]);
        Assert.Null(publishedPayload["Metadata"]);
        Assert.Null(publishedPayload["ExecutionTimeMs"]);
        Assert.Null(publishedPayload["Error"]);
        Assert.True(publisher.ResultMetadata.Succeeded);
    }

    private sealed class RecordingOrchestrationClientPublisher : IOrchestrationClientPublisher
    {
        private readonly IOrchestrationExecutionResultMetadataAccessor _metadataAccessor;

        public RecordingOrchestrationClientPublisher(IOrchestrationExecutionResultMetadataAccessor metadataAccessor)
        {
            _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
        }

        public object? Payload { get; private set; }

        public OrchestrationExecutionResultMetadata? ResultMetadata { get; private set; }

        public Task PublishAsync(
            object payload,
            OrchestrationReplyAddress address,
            CancellationToken cancellationToken = default)
        {
            Payload = payload;
            ResultMetadata = _metadataAccessor.Get();
            return Task.CompletedTask;
        }
    }

    private sealed record ReserveInventoryRequest;

    private sealed record ReserveInventoryResponse;
}
