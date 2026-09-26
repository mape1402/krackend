namespace Krackend.Sagas.Orchestrations.Tests.Client;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pelican.Mediator;
using Spider.Pipelines.Core;
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

    public sealed record NoResponsePipelineRequest(string Id) : IRequest;

    public sealed class NoResponsePipelineRequestHandler : IRequestHandler<NoResponsePipelineRequest>
    {
        public Task Handle(NoResponsePipelineRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
