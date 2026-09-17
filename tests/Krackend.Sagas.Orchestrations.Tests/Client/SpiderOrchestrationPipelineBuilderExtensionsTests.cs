using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pelican.Mediator;
using Spider.Pipelines.Core;
using Spider.Pipelines.PostProcessing;
using Spider.Pipelines.PreProcessing;
using System.Reflection;

namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class SpiderOrchestrationPipelineBuilderExtensionsTests
{
    [Fact]
    public void RequestPipelineOverloadsRegisterPipelineHooks()
    {
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Any<Action<IPostProcessConfiguration<PipelineRequest>>>()).Returns(builder);

        Assert.Same(builder, builder.UseOrchestration());
        Assert.Same(builder, builder.UseOrchestration("events.sales.created", " "));
        Assert.Same(builder, builder.UseOrchestration(static request => new { request.Id }));
    }

    [Fact]
    public void ResponsePipelineOverloadsRegisterPipelineHooks()
    {
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Any<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>()).Returns(builder);

        Assert.Same(builder, builder.UseOrchestration());
        Assert.Same(builder, builder.UseOrchestration("events.sales.completed", " "));
        Assert.Same(builder, builder.UseOrchestration(static response => new { response.Ok }));
        Assert.Same(builder, builder.UseOrchestration(static response => new { response.Ok }, "events.sales.completed"));
    }

    [Fact]
    public void ServiceBridgeOverloadsAttachOrchestrationPipelines()
    {
        var bridge = Substitute.For<IServiceBridge<IMediator>>();
        var requestBridge = Substitute.For<IServiceBridge<IMediator, PipelineRequest>>();
        var responseBridge = Substitute.For<IServiceBridge<IMediator, PipelineRequest, PipelineResponse>>();
        bridge.Attach<PipelineRequest>(Arg.Any<Action<IPipelineBuilder<PipelineRequest>>>()).Returns(requestBridge);
        bridge.Attach<PipelineRequest, PipelineResponse>(Arg.Any<Action<IPipelineBuilder<PipelineRequest, PipelineResponse>>>()).Returns(responseBridge);

        Assert.Same(requestBridge, bridge.UseOrchestration<PipelineRequest>());
        Assert.Same(requestBridge, bridge.UseOrchestration<PipelineRequest>("events.sales.created"));
        Assert.Same(requestBridge, bridge.UseOrchestration<PipelineRequest>(static request => new { request.Id }));
        Assert.Same(requestBridge, bridge.UseOrchestration<PipelineRequest>(static request => new { request.Id }, "events.sales.created"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>());
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>("events.sales.completed"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static response => new { response.Ok }));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static response => new { response.Ok }, "events.sales.completed"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static (request, response) => new { request.Id, response.Ok }));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static (request, response) => new { request.Id, response.Ok }, "events.sales.completed"));
    }

    [Fact]
    public async Task RequestPipelineBeginsReportsSuccessWithTriggerOptionsAndCloses()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportSuccessAsync(
                Arg.Any<Type>(),
                Arg.Any<Type>(),
                Arg.Any<object>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();
        Action<IPreProcessConfiguration<PipelineRequest>> configurePre = null!;
        Action<IPostProcessConfiguration<PipelineRequest>> configurePost = null!;
        builder.OnPreProcess(Arg.Do<Action<IPreProcessConfiguration<PipelineRequest>>>(action => configurePre = action))
            .Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest>>>(action => configurePost = action))
            .Returns(builder);

        var returned = builder.UseOrchestration(
            static request => new { request.Id },
            "events.sales.created",
            "");
        var context = new Context<PipelineRequest>(new PipelineRequest("sale-1"), provider, CancellationToken.None);
        PreProcessDelegate<PipelineRequest> preDelegate = null!;
        SuccessPostProcessDelegate<PipelineRequest> successDelegate = null!;
        var pre = Substitute.For<IPreProcessConfiguration<PipelineRequest>>();
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest>>();
        pre.OnPreProcess(Arg.Do<PreProcessDelegate<PipelineRequest>>(handler => preDelegate = handler))
            .Returns(pre);
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest>>(handler => successDelegate = handler))
            .Returns(post);
        configurePre(pre);
        configurePost(post);

        await preDelegate(context, new PreProcessArguments());
        await successDelegate(context, new PostProcessArguments());

        Assert.Same(builder, returned);
        client.Received(1).Begin(typeof(PipelineRequest));
        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            null,
            Arg.Is<object>(payload => JsonSerializer.Serialize(payload).Contains("sale-1", StringComparison.Ordinal)),
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.created", "1.0.0")),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public async Task RequestPipelineReportsFailureAndCloses()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportFailureAsync(
                Arg.Any<Type>(),
                Arg.Any<Exception>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();
        Action<IPostProcessConfiguration<PipelineRequest>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest>>>(action => configurePost = action))
            .Returns(builder);
        var exception = new InvalidOperationException("failed");
        var context = new Context<PipelineRequest>(new PipelineRequest("sale-2"), provider, CancellationToken.None);
        context.SetException(exception);

        builder.UseOrchestration("events.sales.failed", "2.0.0");
        FailurePostProcessDelegate<PipelineRequest> failureDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest>>();
        post.OnFailure(Arg.Do<FailurePostProcessDelegate<PipelineRequest>>(handler => failureDelegate = handler))
            .Returns(post);
        configurePost(post);
        await failureDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportFailureAsync(
            typeof(PipelineRequest),
            exception,
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.failed", "2.0.0")),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public async Task ResponsePipelineReportsTransformedResponsePayload()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportSuccessAsync(
                Arg.Any<Type>(),
                Arg.Any<Type>(),
                Arg.Any<object>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPreProcessConfiguration<PipelineRequest>> configurePre = null!;
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Do<Action<IPreProcessConfiguration<PipelineRequest>>>(action => configurePre = action))
            .Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-3"), provider, CancellationToken.None);
        context.SetResponse(new PipelineResponse(true));

        builder.UseOrchestration(static (request, response) => new { request.Id, response.Ok }, "events.sales.completed", "3.0.0");
        PreProcessDelegate<PipelineRequest> preDelegate = null!;
        SuccessPostProcessDelegate<PipelineRequest, PipelineResponse> successDelegate = null!;
        var pre = Substitute.For<IPreProcessConfiguration<PipelineRequest>>();
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        pre.OnPreProcess(Arg.Do<PreProcessDelegate<PipelineRequest>>(handler => preDelegate = handler))
            .Returns(pre);
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest, PipelineResponse>>(handler => successDelegate = handler))
            .Returns(post);
        configurePre(pre);
        configurePost(post);
        await preDelegate(context, new PreProcessArguments());
        await successDelegate(context, new PostProcessArguments());

        client.Received(1).Begin(typeof(PipelineRequest));
        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            typeof(PipelineResponse),
            Arg.Is<object>(payload => JsonSerializer.Serialize(payload).Contains("\"Ok\":true", StringComparison.OrdinalIgnoreCase)),
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.completed", "3.0.0")),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public async Task ResponsePipelineReportsFailureAndCloses()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportFailureAsync(
                Arg.Any<Type>(),
                Arg.Any<Exception>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var exception = new ApplicationException("response failed");
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-4"), provider, CancellationToken.None);
        context.SetException(exception);

        builder.UseOrchestration<PipelineRequest, PipelineResponse>("events.sales.failed", "4.0.0");
        FailurePostProcessDelegate<PipelineRequest> failureDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnFailure(Arg.Do<FailurePostProcessDelegate<PipelineRequest>>(handler => failureDelegate = handler))
            .Returns(post);
        configurePost(post);
        await failureDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportFailureAsync(
            typeof(PipelineRequest),
            exception,
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.failed", "4.0.0")),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public void ResponsePipelineRejectsNullRequestResponseTransformer()
    {
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();

        Assert.Throws<ArgumentNullException>(() => builder.UseOrchestration((Func<PipelineRequest, PipelineResponse, object>)null!));
    }

    [Fact]
    public void PrivateResponsePipelineCoreRejectsNullTransformDelegate()
    {
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        var method = typeof(OrchestrationPipelineBuilderExtensions)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "UseOrchestration" && method.GetGenericArguments().Length == 3)
            .MakeGenericMethod(typeof(PipelineRequest), typeof(PipelineResponse), typeof(Func<PipelineResponse, object>));

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
            null,
            [
                builder,
                null!,
                (Func<PipelineResponse, object>)(static response => response),
                "events.sales.completed",
                "1.0.0"
            ]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    private static bool MatchesTrigger(OrchestrationOperationOptions options, string topic, string version)
        {
            if (options?.TriggerAddress is null ||
                options.TriggerAddress.Transport != OrchestrationTransportNames.Messaging)
            {
                return false;
            }

            var settings = JsonSerializer.Deserialize<MessagingReplyAddressSettings>(
                options.TriggerAddress.SettingsPayload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return settings?.Topic == topic &&
                settings.Version == version;
        }

    public sealed record PipelineRequest(string Id);

    public sealed record PipelineResponse(bool Ok);
}
