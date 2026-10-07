using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Client.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pelican.Mediator;
using Spider.Pipelines.Core;
using Spider.Pipelines.PostProcessing;
using Spider.Pipelines.PreProcessing;
using System.Reflection;
using System.Text.Json.Nodes;
using SquirrelBox;

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
        Assert.Same(builder, builder.UseOrchestration(routes => routes.When(static _ => true, "events.sales.created")));
        Assert.Same(builder, builder.EmitEvent("events.sales.created", " "));
        Assert.Same(builder, builder.EmitEvent(static request => new { request.Id }, "events.sales.created"));
        Assert.Same(builder, builder.EmitEvent(routes => routes.When(static _ => true, "events.sales.created")));
    }

    [Fact]
    public void RequestPipelineRoutingRejectsNullArguments()
    {
        IPipelineBuilder<PipelineRequest> nullBuilder = null!;
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();

        Assert.Throws<ArgumentNullException>(() => nullBuilder.UseOrchestration(routes => routes.When(static _ => true, "events.sales.created")));
        Assert.Throws<ArgumentNullException>(() => builder.UseOrchestration((Action<OrchestrationTriggerRouteBuilder<PipelineRequest>>)null!));
        Assert.Throws<ArgumentNullException>(() => nullBuilder.EmitEvent(routes => routes.When(static _ => true, "events.sales.created")));
        Assert.Throws<ArgumentNullException>(() => builder.EmitEvent((Action<OrchestrationTriggerRouteBuilder<PipelineRequest>>)null!));
    }

    [Fact]
    public void RequestPipelineCanComposeEmitEventAndUseOrchestrationHooks()
    {
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Any<Action<IPostProcessConfiguration<PipelineRequest>>>()).Returns(builder);

        Assert.Same(builder, builder.EmitEvent("events.sales.created").UseOrchestration());

        builder.Received(2).OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>());
        builder.Received(2).OnPostProcess(Arg.Any<Action<IPostProcessConfiguration<PipelineRequest>>>());
    }

    [Fact]
    public void RequestRouteBuilderResolvesRoutesFallbacksAndRequiredMatches()
    {
        var customAddress = new OrchestrationReplyAddress
        {
            Transport = "custom",
            SettingsPayload = "{}"
        };
        var builder = new OrchestrationTriggerRouteBuilder<PipelineRequest>()
            .When(
                static request => request.Id == "sale-1",
                static request => new { request.Id, Routed = true },
                "events.sales.created",
                "")
            .When(static request => request.Id == "sale-custom", customAddress)
            .Otherwise("events.sales.fallback", "2.0.0");

        var matched = builder.Resolve(new PipelineRequest("sale-1"));
        var custom = builder.Resolve(new PipelineRequest("sale-custom"));
        var fallback = builder.Resolve(new PipelineRequest("other"));
        var nullAddress = new OrchestrationTriggerRouteBuilder<PipelineRequest>()
            .Otherwise(customAddress)
            .Resolve(new PipelineRequest("anything"));

        Assert.True(matched.Matched);
        Assert.Contains("\"routed\":true", JsonSerializer.Serialize(matched.Payload), StringComparison.OrdinalIgnoreCase);
        Assert.True(MatchesTrigger(matched.Options, "events.sales.created", "1.0.0"));
        Assert.True(custom.Matched);
        Assert.Same(customAddress, custom.Options.TriggerAddress);
        Assert.True(fallback.Matched);
        Assert.True(IsPipelineRequestPayload(fallback.Payload, "other"));
        Assert.True(MatchesTrigger(fallback.Options, "events.sales.fallback", "2.0.0"));
        Assert.True(nullAddress.Matched);
        Assert.Same(customAddress, nullAddress.Options.TriggerAddress);
        Assert.Null(new OrchestrationTriggerRouteBuilder<PipelineRequest>()
            .Otherwise(" ")
            .Resolve(new PipelineRequest("anything"))
            .Options.TriggerAddress);
        Assert.False(new OrchestrationTriggerRouteBuilder<PipelineRequest>().Resolve(new PipelineRequest("none")).Matched);
        Assert.Throws<OrchestrationTriggerRouteMatchException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest>()
                .RequireMatch()
                .Resolve(new PipelineRequest("none")));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest>()
                .When((Func<PipelineRequest, bool>)null!, "events.sales.created"));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest>()
                .When(static _ => true, (Func<PipelineRequest, object>)null!, "events.sales.created"));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest>()
                .Otherwise((Func<PipelineRequest, object>)null!, "events.sales.created"));
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
        Assert.Same(builder, builder.UseOrchestration(routes => routes.When(static (_, _) => true, "events.sales.completed")));
        Assert.Same(builder, builder.EmitEvent("events.sales.completed", " "));
        Assert.Same(builder, builder.EmitEvent(static response => new { response.Ok }, "events.sales.completed"));
        Assert.Same(builder, builder.EmitEvent(static (request, response) => new { request.Id, response.Ok }, "events.sales.completed"));
        Assert.Same(builder, builder.EmitEvent(routes => routes.When(static (_, _) => true, "events.sales.completed")));
    }

    [Fact]
    public void ResponsePipelineRoutingRejectsNullArguments()
    {
        IPipelineBuilder<PipelineRequest, PipelineResponse> nullBuilder = null!;
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();

        Assert.Throws<ArgumentNullException>(() => nullBuilder.UseOrchestration(routes => routes.When(static (_, _) => true, "events.sales.completed")));
        Assert.Throws<ArgumentNullException>(() => builder.UseOrchestration((Action<OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>>)null!));
        Assert.Throws<ArgumentNullException>(() => nullBuilder.EmitEvent(routes => routes.When(static (_, _) => true, "events.sales.completed")));
        Assert.Throws<ArgumentNullException>(() => builder.EmitEvent((Action<OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>>)null!));
    }

    [Fact]
    public void ResponseRouteBuilderResolvesRoutesFallbacksAndRequiredMatches()
    {
        var customAddress = new OrchestrationReplyAddress
        {
            Transport = "custom",
            SettingsPayload = "{}"
        };
        var builder = new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
            .When(
                static (request, response) => request.Id == "sale-1" && response.Ok,
                static (request, response) => new { request.Id, response.Ok },
                "events.sales.completed",
                "")
            .When(static (request, _) => request.Id == "sale-custom", customAddress)
            .Otherwise(static (_, response) => response, "events.sales.fallback", "3.0.0");

        var matched = builder.Resolve(new PipelineRequest("sale-1"), new PipelineResponse(true));
        var custom = builder.Resolve(new PipelineRequest("sale-custom"), new PipelineResponse(true));
        var fallback = builder.Resolve(new PipelineRequest("other"), new PipelineResponse(false));
        var nullAddress = new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
            .Otherwise(customAddress)
            .Resolve(new PipelineRequest("anything"), new PipelineResponse(true));

        Assert.True(matched.Matched);
        Assert.Contains("\"ok\":true", JsonSerializer.Serialize(matched.Payload), StringComparison.OrdinalIgnoreCase);
        Assert.True(MatchesTrigger(matched.Options, "events.sales.completed", "1.0.0"));
        Assert.True(custom.Matched);
        Assert.Same(customAddress, custom.Options.TriggerAddress);
        Assert.True(fallback.Matched);
        Assert.Equal(new PipelineResponse(false), fallback.Payload);
        Assert.True(MatchesTrigger(fallback.Options, "events.sales.fallback", "3.0.0"));
        Assert.True(nullAddress.Matched);
        Assert.Same(customAddress, nullAddress.Options.TriggerAddress);
        Assert.Null(new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
            .Otherwise(" ")
            .Resolve(new PipelineRequest("anything"), new PipelineResponse(true))
            .Options.TriggerAddress);
        Assert.False(new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
            .Resolve(new PipelineRequest("none"), new PipelineResponse(false))
            .Matched);
        Assert.Throws<OrchestrationTriggerRouteMatchException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
                .RequireMatch()
                .Resolve(new PipelineRequest("none"), new PipelineResponse(false)));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
                .When((Func<PipelineRequest, PipelineResponse, bool>)null!, "events.sales.completed"));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
                .When(static (_, _) => true, (Func<PipelineRequest, PipelineResponse, object>)null!, "events.sales.completed"));
        Assert.Throws<ArgumentNullException>(() =>
            new OrchestrationTriggerRouteBuilder<PipelineRequest, PipelineResponse>()
                .Otherwise((Func<PipelineRequest, PipelineResponse, object>)null!, "events.sales.completed"));
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
        Assert.Same(requestBridge, bridge.UseOrchestration<PipelineRequest>(routes => routes.When(static _ => true, "events.sales.created")));
        Assert.Same(requestBridge, bridge.EmitEvent<PipelineRequest>("events.sales.created"));
        Assert.Same(requestBridge, bridge.EmitEvent<PipelineRequest>(static request => new { request.Id }, "events.sales.created"));
        Assert.Same(requestBridge, bridge.EmitEvent<PipelineRequest>(routes => routes.When(static _ => true, "events.sales.created")));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>());
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>("events.sales.completed"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static response => new { response.Ok }));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static response => new { response.Ok }, "events.sales.completed"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static (request, response) => new { request.Id, response.Ok }));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(static (request, response) => new { request.Id, response.Ok }, "events.sales.completed"));
        Assert.Same(responseBridge, bridge.UseOrchestration<PipelineRequest, PipelineResponse>(routes => routes.When(static (_, _) => true, "events.sales.completed")));
        Assert.Same(responseBridge, bridge.EmitEvent<PipelineRequest, PipelineResponse>("events.sales.completed"));
        Assert.Same(responseBridge, bridge.EmitEvent<PipelineRequest, PipelineResponse>(static response => new { response.Ok }, "events.sales.completed"));
        Assert.Same(responseBridge, bridge.EmitEvent<PipelineRequest, PipelineResponse>(static (request, response) => new { request.Id, response.Ok }, "events.sales.completed"));
        Assert.Same(responseBridge, bridge.EmitEvent<PipelineRequest, PipelineResponse>(routes => routes.When(static (_, _) => true, "events.sales.completed")));
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
    public async Task RequestPipelineRoutingReportsFailureAndCompletesWhenBackchannelExists()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportFailureAsync(
                Arg.Any<Type>(),
                Arg.Any<Exception>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var metadataAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        metadataAccessor.Get().Returns(CreateBackchannelMetadata());
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(metadataAccessor)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest>>();
        Action<IPostProcessConfiguration<PipelineRequest>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest>>>(action => configurePost = action))
            .Returns(builder);
        var exception = new InvalidOperationException("routed failure");
        var context = new Context<PipelineRequest>(new PipelineRequest("sale-route-failed"), provider, CancellationToken.None);
        context.SetException(exception);

        builder.UseOrchestration(routes => routes.When(static _ => true, "events.sales.routed"));
        FailurePostProcessDelegate<PipelineRequest> failureDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest>>();
        post.OnFailure(Arg.Do<FailurePostProcessDelegate<PipelineRequest>>(handler => failureDelegate = handler))
            .Returns(post);
        configurePost(post);
        await failureDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportFailureAsync(
            typeof(PipelineRequest),
            exception,
            Arg.Is<OrchestrationOperationOptions>(options => HasNoTrigger(options)),
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
    public async Task ResponsePipelineReportsFailureAndCompletesWhenBackchannelExists()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportFailureAsync(
                Arg.Any<Type>(),
                Arg.Any<Exception>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var metadataAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        metadataAccessor.Get().Returns(CreateBackchannelMetadata());
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(metadataAccessor)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var exception = new ApplicationException("response failed with backchannel");
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-4b"), provider, CancellationToken.None);
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
    public async Task RequestPipelineRoutingPublishesFirstMatchingRouteWithDefaultPayload()
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
        var context = new Context<PipelineRequest>(new PipelineRequest("sale-route-1"), provider, CancellationToken.None);

        builder.UseOrchestration(routes => routes
            .When(static _ => false, "events.sales.ignored")
            .When(static _ => true, "events.sales.routed"));
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

        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            null,
            Arg.Is<object>(payload => IsPipelineRequestPayload(payload, "sale-route-1")),
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.routed", "1.0.0")),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public async Task ResponsePipelineRoutingReportsFailureAndCompletesWhenBackchannelExists()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportFailureAsync(
                Arg.Any<Type>(),
                Arg.Any<Exception>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var metadataAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        metadataAccessor.Get().Returns(CreateBackchannelMetadata());
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(metadataAccessor)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var exception = new ApplicationException("routed response failed");
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-failed-response"), provider, CancellationToken.None);
        context.SetException(exception);

        builder.UseOrchestration(routes => routes.When(static (_, _) => true, "events.sales.routed"));
        FailurePostProcessDelegate<PipelineRequest> failureDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnFailure(Arg.Do<FailurePostProcessDelegate<PipelineRequest>>(handler => failureDelegate = handler))
            .Returns(post);
        configurePost(post);
        await failureDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportFailureAsync(
            typeof(PipelineRequest),
            exception,
            Arg.Is<OrchestrationOperationOptions>(options => HasNoTrigger(options)),
            Arg.Any<CancellationToken>());
        client.Received(1).Close();
    }

    [Fact]
    public async Task ResponsePipelineRoutingPublishesTransformedFirstMatchingRoute()
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
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-2"), provider, CancellationToken.None);
        context.SetResponse(new PipelineResponse(true));

        builder.UseOrchestration(routes => routes
            .When(static (_, response) => !response.Ok, "events.sales.rejected")
            .When(
                static (_, response) => response.Ok,
                static (request, response) => new { request.Id, response.Ok, Routed = true },
                "events.sales.approved",
                "2.0.0"));
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

        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            typeof(PipelineResponse),
            Arg.Is<object>(payload => JsonSerializer.Serialize(payload).Contains("\"Routed\":true", StringComparison.OrdinalIgnoreCase)),
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.approved", "2.0.0")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponsePipelineRoutingUsesOtherwiseWhenNoRouteMatches()
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
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-3"), provider, CancellationToken.None);
        context.SetResponse(new PipelineResponse(false));

        builder.UseOrchestration(routes => routes
            .When(static (_, response) => response.Ok, "events.sales.approved")
            .Otherwise(static (request, response) => new { request.Id, response.Ok }, "events.sales.fallback"));
        SuccessPostProcessDelegate<PipelineRequest, PipelineResponse> successDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest, PipelineResponse>>(handler => successDelegate = handler))
            .Returns(post);
        configurePost(post);
        await successDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            typeof(PipelineResponse),
            Arg.Is<object>(payload => JsonSerializer.Serialize(payload).Contains("sale-route-3", StringComparison.Ordinal)),
            Arg.Is<OrchestrationOperationOptions>(options => MatchesTrigger(options, "events.sales.fallback", "1.0.0")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponsePipelineRoutingDoesNotPublishTriggerWhenNoRouteMatches()
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
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-4"), provider, CancellationToken.None);
        context.SetResponse(new PipelineResponse(false));

        builder.UseOrchestration(routes => routes.When(static (_, response) => response.Ok, "events.sales.approved"));
        SuccessPostProcessDelegate<PipelineRequest, PipelineResponse> successDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest, PipelineResponse>>(handler => successDelegate = handler))
            .Returns(post);
        configurePost(post);
        await successDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            typeof(PipelineResponse),
            Arg.Is<object>(payload => payload == null),
            Arg.Is<OrchestrationOperationOptions>(options => HasNoTrigger(options)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponsePipelineRoutingRequireMatchThrowsAndCloses()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-5"), provider, CancellationToken.None);
        context.SetResponse(new PipelineResponse(false));

        builder.UseOrchestration(routes => routes
            .When(static (_, response) => response.Ok, "events.sales.approved")
            .RequireMatch());
        SuccessPostProcessDelegate<PipelineRequest, PipelineResponse> successDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest, PipelineResponse>>(handler => successDelegate = handler))
            .Returns(post);
        configurePost(post);

        await Assert.ThrowsAsync<OrchestrationTriggerRouteMatchException>(() => successDelegate(context, new PostProcessArguments()));
        client.Received(1).Close();
    }

    [Fact]
    public async Task ResponsePipelineRoutingIgnoresRoutesWhenBackchannelMetadataExists()
    {
        var client = Substitute.For<IOrchestrationOperationClient>();
        client.ReportSuccessAsync(
                Arg.Any<Type>(),
                Arg.Any<Type>(),
                Arg.Any<object>(),
                Arg.Any<OrchestrationOperationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var metadataAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        metadataAccessor.Get().Returns(new OrchestrationMessageMetadata
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{}"
            }
        });
        var provider = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(metadataAccessor)
            .BuildServiceProvider();
        var builder = Substitute.For<IPipelineBuilder<PipelineRequest, PipelineResponse>>();
        Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>> configurePost = null!;
        builder.OnPreProcess(Arg.Any<Action<IPreProcessConfiguration<PipelineRequest>>>()).Returns(builder);
        builder.OnPostProcess(Arg.Do<Action<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>>(action => configurePost = action))
            .Returns(builder);
        var response = new PipelineResponse(true);
        var context = new Context<PipelineRequest, PipelineResponse>(new PipelineRequest("sale-route-6"), provider, CancellationToken.None);
        context.SetResponse(response);

        builder.UseOrchestration(routes => routes
            .When(static (_, _) => false, "events.sales.unreachable")
            .RequireMatch());
        SuccessPostProcessDelegate<PipelineRequest, PipelineResponse> successDelegate = null!;
        var post = Substitute.For<IPostProcessConfiguration<PipelineRequest, PipelineResponse>>();
        post.OnSuccess(Arg.Do<SuccessPostProcessDelegate<PipelineRequest, PipelineResponse>>(handler => successDelegate = handler))
            .Returns(post);
        configurePost(post);
        await successDelegate(context, new PostProcessArguments());

        await client.Received(1).ReportSuccessAsync(
            typeof(PipelineRequest),
            typeof(PipelineResponse),
            response,
            Arg.Is<OrchestrationOperationOptions>(options => HasNoTrigger(options)),
            Arg.Any<CancellationToken>());
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
            .Single(method => method.Name == "AttachResponseOrchestration" && method.GetGenericArguments().Length == 3)
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

    [Fact]
    public void PrivateRestoreDeferredMetadataCoversInboxFallbacksAndReservedKeys()
    {
        var inboxAccessor = new TestInboxContextAccessor();
        var messageMetadata = CreateBackchannelMetadata();
        var propagationMetadata = new OrchestrationPropagationMetadata();
        propagationMetadata.Items["tenant"] = JsonValue.Create("north")!;
        inboxAccessor.Current = new InboxContext(
            new InboxEntry
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
                    [" "] = "ignored",
                    [OrchestrationMetadataConstants.TriggerMetadataKey] = """{"trigger":true}""",
                    ["Krackend.Sagas.Orchestrations.Internal"] = "ignored"
                }
            },
            "test",
            ownsCompletion: false,
            previous: null);
        using var provider = CreateClientProvider(inboxAccessor);

        InvokePrivateStatic("RestoreDeferredMessageMetadata", provider);

        var restoredMessage = provider.GetRequiredService<IOrchestrationMessageMetadataAccessor>().Get();
        var restoredPropagation = provider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get();
        Assert.NotNull(restoredMessage?.ReplyAddress);
        Assert.Equal("north", restoredPropagation.Items["tenant"]!.GetValue<string>());
        Assert.Equal(42, restoredPropagation.Items["orderId"]!.GetValue<int>());
        Assert.Equal("hello", restoredPropagation.Items["rawText"]!.GetValue<string>());
        Assert.True(restoredPropagation.Items.ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey));
        Assert.False(restoredPropagation.Items.ContainsKey("Krackend.Sagas.Orchestrations.Internal"));
        Assert.False(restoredPropagation.Items.ContainsKey(" "));
    }

    [Fact]
    public void PrivateRestoreDeferredMetadataStopsWhenStateAlreadyExistsOrInboxIsEmpty()
    {
        var inboxAccessor = new TestInboxContextAccessor
        {
            Current = new InboxContext(new InboxEntry(), "test", ownsCompletion: false, previous: null)
        };
        using var provider = CreateClientProvider(inboxAccessor);
        provider.GetRequiredService<IOrchestrationMessageMetadataSetter>().Set(CreateBackchannelMetadata());
        var existingPropagation = new OrchestrationPropagationMetadata();
        existingPropagation.Items["existing"] = JsonValue.Create(true)!;
        provider.GetRequiredService<IOrchestrationPropagationMetadataSetter>().Set(existingPropagation);

        InvokePrivateStatic("RestoreDeferredMessageMetadata", provider);

        Assert.True(provider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().Items["existing"]!.GetValue<bool>());
        Assert.NotNull(provider.GetRequiredService<IOrchestrationMessageMetadataAccessor>().Get().ReplyAddress);

        using var emptyProvider = CreateClientProvider(new TestInboxContextAccessor());
        InvokePrivateStatic("RestoreDeferredPropagationMetadata", emptyProvider);
        Assert.False(emptyProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().HasItems);

        using var emptyInboxProvider = CreateClientProvider(new TestInboxContextAccessor
        {
            Current = new InboxContext(new InboxEntry(), "test", ownsCompletion: false, previous: null)
        });
        InvokePrivateStatic("RestoreDeferredMessageMetadata", emptyInboxProvider);
        Assert.False(emptyInboxProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().HasItems);
    }

    [Fact]
    public void PrivateRestoreDeferredMetadataIgnoresInvalidSerializedEnvelopes()
    {
        var inboxAccessor = new TestInboxContextAccessor
        {
            Current = new InboxContext(
                new InboxEntry
                {
                    Metadata =
                    {
                        [OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] = "{}",
                        [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = "{not-json",
                        ["fallback"] = "not-json"
                    }
                },
                "test",
                ownsCompletion: false,
                previous: null)
        };
        using var provider = CreateClientProvider(inboxAccessor);

        InvokePrivateStatic("RestoreDeferredMessageMetadata", provider);

        Assert.Null(provider.GetRequiredService<IOrchestrationMessageMetadataAccessor>().Get().ReplyAddress);
        Assert.Equal("not-json", provider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().Items["fallback"]!.GetValue<string>());

        using var nullEnvelopeProvider = CreateClientProvider(new TestInboxContextAccessor
        {
            Current = new InboxContext(
                new InboxEntry
                {
                    Metadata =
                    {
                        [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = "null"
                    }
                },
                "test",
                ownsCompletion: false,
                previous: null)
        });
        InvokePrivateStatic("RestoreDeferredPropagationMetadata", nullEnvelopeProvider);
        Assert.False(nullEnvelopeProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().HasItems);

        var nullablePropagation = new OrchestrationPropagationMetadata();
        nullablePropagation.Items["nullable"] = null!;
        using var nullableEnvelopeProvider = CreateClientProvider(new TestInboxContextAccessor
        {
            Current = new InboxContext(
                new InboxEntry
                {
                    Metadata =
                    {
                        [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
                            JsonSerializer.Serialize(nullablePropagation, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    }
                },
                "test",
                ownsCompletion: false,
                previous: null)
        });
        InvokePrivateStatic("RestoreDeferredPropagationMetadata", nullableEnvelopeProvider);
        Assert.True(nullableEnvelopeProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().Items.ContainsKey("nullable"));

        using var rawOnlyProvider = CreateClientProvider(new TestInboxContextAccessor
        {
            Current = new InboxContext(
                new InboxEntry
                {
                    Metadata =
                    {
                        ["rawOnly"] = """{"kept":true}"""
                    }
                },
                "test",
                ownsCompletion: false,
                previous: null)
        });
        InvokePrivateStatic("RestoreDeferredPropagationMetadata", rawOnlyProvider);
        Assert.True(rawOnlyProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>().Get().Items["rawOnly"]!["kept"]!.GetValue<bool>());
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

    private static bool HasNoTrigger(OrchestrationOperationOptions options)
        => options is not null && options.TriggerAddress is null;

    private static bool IsPipelineRequestPayload(object payload, string id)
        => payload is PipelineRequest request && request.Id == id;

    private static OrchestrationMessageMetadata CreateBackchannelMetadata()
        => new()
        {
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = "{}"
            }
        };

    private static ServiceProvider CreateClientProvider(IInboxContextAccessor inboxAccessor)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient();
        services.AddSingleton(inboxAccessor);
        return services.BuildServiceProvider();
    }

    private static void InvokePrivateStatic(string methodName, params object?[] args)
    {
        var method = Assert.Single(typeof(OrchestrationPipelineBuilderExtensions)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length));
        method.Invoke(null, args);
    }

    private sealed class TestInboxContextAccessor : IInboxContextAccessor
    {
        public InboxContext Current { get; set; } = null!;

        public void Prepare()
        {
        }
    }

    public sealed record PipelineRequest(string Id);

    public sealed record PipelineResponse(bool Ok);
}
