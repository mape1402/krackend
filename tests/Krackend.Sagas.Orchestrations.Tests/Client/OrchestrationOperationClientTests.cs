namespace Krackend.Sagas.Orchestrations.Tests.Client;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;

public sealed class OrchestrationOperationClientTests
{
    [Fact]
    public async Task GenericReportSuccessAsyncDelegatesToPublisherWithDefaultOptions()
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
        client.Begin<ReserveInventoryRequest>();

        await client.ReportSuccessAsync<ReserveInventoryRequest, ReserveInventoryResponse>(
            new ReserveInventoryResponse());

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.True(publisher.ResultMetadata.Succeeded);
        Assert.Equal(typeof(ReserveInventoryRequest).FullName, publisher.ResultMetadata.RequestType);
        Assert.Equal(typeof(ReserveInventoryResponse).FullName, publisher.ResultMetadata.ResponseType);
        Assert.IsAssignableFrom<JsonNode>(publisher.Payload);
    }

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
    public async Task ReportFailureAsync_WhenExceptionDetailsAreDisabled_PublishesRedactedFailureMetadata()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient(options =>
        {
            options.IncludeExceptionDetails = false;
            options.RedactedExceptionMessage = "Inventory operation failed.";
            options.Map<InvalidOperationException>("InventoryFailure", isRetryableCandidate: true);
        });
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
        client.Begin<ReserveInventoryRequest>();

        await client.ReportFailureAsync<ReserveInventoryRequest>(
            new InvalidOperationException("Database password leaked in an internal message."));

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.False(publisher.ResultMetadata.Succeeded);
        Assert.Equal("InventoryFailure", publisher.ResultMetadata.ErrorCode);
        Assert.Equal("Inventory operation failed.", publisher.ResultMetadata.ErrorMessage);
        Assert.Null(publisher.ResultMetadata.ErrorType);
        Assert.True(publisher.ResultMetadata.IsRetryableCandidate);
        Assert.Null(publisher.Payload);
    }

    [Fact]
    public async Task ReportFailureAsync_WhenSpecificPredicateMatches_PublishesPredicateErrorCode()
    {
        var services = new ServiceCollection();
        services
            .AddKrackendOrchestrationsClient()
            .MapException<InvalidOperationException>("GenericInvalidOperation")
            .MapException<InvalidOperationException>(
                "TransientInventoryUnavailable",
                exception => exception.Message.Contains("transient", StringComparison.OrdinalIgnoreCase),
                isRetryableCandidate: true);
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
        client.Begin<ReserveInventoryRequest>();

        await client.ReportFailureAsync<ReserveInventoryRequest>(
            new InvalidOperationException("transient inventory outage"));

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.Equal("TransientInventoryUnavailable", publisher.ResultMetadata.ErrorCode);
        Assert.True(publisher.ResultMetadata.IsRetryableCandidate);
        Assert.Null(publisher.Payload);
    }

    [Fact]
    public async Task ReportFailureAsync_WhenNoMappingMatches_PublishesDefaultErrorCode()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient(options => options.DefaultErrorCode = "UnhandledClientFailure");
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
        client.Begin<ReserveInventoryRequest>();

        await client.ReportFailureAsync<ReserveInventoryRequest>(
            new ApplicationException("unmapped failure"));

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.NotNull(publisher.ResultMetadata);
        Assert.Equal("UnhandledClientFailure", publisher.ResultMetadata.ErrorCode);
        Assert.Null(publisher.Payload);
    }

    [Fact]
    public async Task ReportFailureAsync_WhenNoBackchannelExists_DoesNotPublishFailure()
    {
        var services = new ServiceCollection();
        services
            .AddKrackendOrchestrationsClient()
            .MapException<InvalidOperationException>("FailureWithoutBackchannel");
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        client.Begin<ReserveInventoryRequest>();

        await client.ReportFailureAsync<ReserveInventoryRequest>(
            new InvalidOperationException("consumer used outside orchestration"));

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.Equal(0, publisher.PublishCount);
        Assert.Null(publisher.ResultMetadata);
        Assert.Null(publisher.Payload);
    }

    [Fact]
    public async Task ReportSuccessAsync_WhenNoBackchannelExistsAndTriggerDestinationIsConfigured_PublishesTriggerPayload()
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        var payload = JsonNode.Parse("""{"saleId":"sale-1"}""");

        await client.ReportSuccessAsync(
            typeof(ReserveInventoryRequest),
            typeof(ReserveInventoryResponse),
            payload!,
            new OrchestrationOperationOptions
            {
                TriggerAddress = new OrchestrationReplyAddress
                {
                    Transport = OrchestrationTransportNames.Messaging,
                    SettingsPayload = """{"topic":"events.sales.sale.created","version":"1.0.0"}"""
                }
            });

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        Assert.Equal(1, publisher.PublishCount);
        Assert.Equal(payload!.ToJsonString(), Assert.IsAssignableFrom<JsonNode>(publisher.Payload).ToJsonString());
        Assert.Null(publisher.ResultMetadata);
        Assert.Equal(OrchestrationTransportNames.Messaging, publisher.Address?.Transport);
        Assert.Contains("events.sales.sale.created", publisher.Address?.SettingsPayload);
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

    [Fact]
    public void ErrorMappingOptionsValidateExceptionTypesAndReplaceDefaultMappings()
    {
        var options = new Krackend.Sagas.Orchestrations.Client.Errors.OrchestrationClientErrorMappingOptions();

        Assert.Throws<ArgumentNullException>(() =>
            options.Map<InvalidOperationException>("Invalid", (Func<InvalidOperationException, bool>)null!));
        Assert.Throws<ArgumentNullException>(() => options.Map(null!, "Invalid"));
        Assert.Throws<ArgumentException>(() => options.Map(typeof(string), "Invalid"));

        options.Map<InvalidOperationException>("First");
        options.Map<InvalidOperationException>("Second");
        options.Map<InvalidOperationException>("Predicate", exception => exception.Message.Contains("match", StringComparison.Ordinal));

        Assert.Equal(2, options.Mappings.Count);
        Assert.Contains(options.Mappings, mapping => mapping.ErrorCode == "Second" && mapping.Predicate is null);
        Assert.Contains(options.Mappings, mapping => mapping.ErrorCode == "Predicate" && mapping.Predicate is not null);
    }

    private sealed record ReserveInventoryRequest;

    private sealed record ReserveInventoryResponse;
}
