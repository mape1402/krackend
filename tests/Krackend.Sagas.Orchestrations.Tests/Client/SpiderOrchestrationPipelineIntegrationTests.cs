namespace Krackend.Sagas.Orchestrations.Tests.Client;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pelican.Mediator;
using SquirrelBox;
using Spider.Pipelines.Core;
using System.Text.Json.Nodes;
using System.Text.Json;
using TurtlePath.Spider;

public sealed class SpiderOrchestrationPipelineIntegrationTests
{
    [Fact]
    public async Task NoResponseBridgePublishesOrchestrationCallbackAfterSuccessfulSend()
    {
        var services = new ServiceCollection();
        services.AddSpider();
        services.AddPelican(typeof(SpiderOrchestrationPipelineIntegrationTests).Assembly);
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.2"}"""
            }
        });

        var spider = scope.ServiceProvider.GetRequiredService<ISpider>();
        await spider
            .AsMediator()
            .UseOrchestration<NoResponsePipelineRequest>()
            .Send(new NoResponsePipelineRequest("sale-1"), CancellationToken.None);

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider
            .GetRequiredService<IOrchestrationClientPublisher>();

        Assert.Equal(1, publisher.PublishCount);
        Assert.NotNull(publisher.ResultMetadata);
        Assert.True(publisher.ResultMetadata.Succeeded);
        Assert.Equal(typeof(NoResponsePipelineRequest).FullName, publisher.ResultMetadata.RequestType);
        Assert.Null(publisher.ResultMetadata.ResponseType);
    }

    [Fact]
    public async Task NoResponseBridgeRestoresOrchestrationMetadataFromDeferredSquirrelBoxInbox()
    {
        var services = new ServiceCollection();
        services.AddSpider();
        services.AddPelican(typeof(SpiderOrchestrationPipelineIntegrationTests).Assembly);
        services.AddKrackendOrchestrationsClient();
        var inboxAccessor = new TestInboxContextAccessor();
        services.AddSingleton<IInboxContextAccessor>(inboxAccessor);
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var messageMetadata = new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.2"}"""
            }
        };
        var propagationMetadata = new OrchestrationPropagationMetadata();
        propagationMetadata.Items["tenant"] = JsonValue.Create("north")!;

        var inboxEntry = new InboxEntry
        {
            Metadata =
            {
                [OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] =
                    JsonSerializer.Serialize(messageMetadata, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
                    JsonSerializer.Serialize(propagationMetadata, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ["tenant"] = "south",
                ["orderId"] = "42",
                ["rawText"] = "hello",
                ["emptyValue"] = "",
                ["Krackend.Sagas.Orchestrations.Internal"] = "ignored"
            }
        };

        inboxAccessor.Current = new InboxContext(inboxEntry, "test", ownsCompletion: false, previous: null);

        var spider = scope.ServiceProvider.GetRequiredService<ISpider>();
        await spider
            .AsMediator()
            .UseOrchestration<NoResponsePipelineRequest>()
            .Send(new NoResponsePipelineRequest("sale-1"), CancellationToken.None);

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider
            .GetRequiredService<IOrchestrationClientPublisher>();

        Assert.Equal(1, publisher.PublishCount);
        Assert.NotNull(publisher.ResultMetadata);
        Assert.True(publisher.ResultMetadata.Succeeded);
        Assert.NotNull(publisher.PropagationMetadata);
        Assert.Equal("north", publisher.PropagationMetadata.Items["tenant"]!.GetValue<string>());
        Assert.Equal(42, publisher.PropagationMetadata.Items["orderId"]!.GetValue<int>());
        Assert.Equal("hello", publisher.PropagationMetadata.Items["rawText"]!.GetValue<string>());
        Assert.Equal(string.Empty, publisher.PropagationMetadata.Items["emptyValue"]!.GetValue<string>());
        Assert.False(publisher.PropagationMetadata.Items.ContainsKey("Krackend.Sagas.Orchestrations.Internal"));
    }

    [Fact]
    public async Task NoResponseBridgePublishesFailureAndCompletesPipelineWhenBackchannelExists()
    {
        var services = new ServiceCollection();
        services.AddSpider();
        services.AddPelican(typeof(SpiderOrchestrationPipelineIntegrationTests).Assembly);
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var metadataSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        metadataSetter.Set(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.2"}"""
            }
        });

        var spider = scope.ServiceProvider.GetRequiredService<ISpider>();
        await spider
            .AsMediator()
            .UseOrchestration<FailingNoResponsePipelineRequest>()
            .Send(new FailingNoResponsePipelineRequest("sale-1"), CancellationToken.None);

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider
            .GetRequiredService<IOrchestrationClientPublisher>();

        Assert.Equal(1, publisher.PublishCount);
        Assert.NotNull(publisher.ResultMetadata);
        Assert.False(publisher.ResultMetadata.Succeeded);
        Assert.Equal("Failed", publisher.ResultMetadata.Status);
    }

    [Fact]
    public async Task NoResponseBridgeKeepsFailureWhenBackchannelDoesNotExist()
    {
        var services = new ServiceCollection();
        services.AddSpider();
        services.AddPelican(typeof(SpiderOrchestrationPipelineIntegrationTests).Assembly);
        services.AddKrackendOrchestrationsClient();
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var spider = scope.ServiceProvider.GetRequiredService<ISpider>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => spider
            .AsMediator()
            .UseOrchestration<FailingNoResponsePipelineRequest>()
            .Send(new FailingNoResponsePipelineRequest("sale-1"), CancellationToken.None));

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider
            .GetRequiredService<IOrchestrationClientPublisher>();

        Assert.Equal(0, publisher.PublishCount);
    }

    public sealed record NoResponsePipelineRequest(string Id) : IRequest;

    public sealed class NoResponsePipelineRequestHandler : IRequestHandler<NoResponsePipelineRequest>
    {
        public Task Handle(NoResponsePipelineRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    public sealed record FailingNoResponsePipelineRequest(string Id) : IRequest;

    public sealed class FailingNoResponsePipelineRequestHandler : IRequestHandler<FailingNoResponsePipelineRequest>
    {
        public Task Handle(FailingNoResponsePipelineRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Inventory failed.");
    }

    private sealed class TestInboxContextAccessor : IInboxContextAccessor
    {
        public InboxContext Current { get; set; } = null!;

        public void Prepare()
        {
        }
    }
}
