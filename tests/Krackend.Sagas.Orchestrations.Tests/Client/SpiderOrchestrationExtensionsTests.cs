using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Client.Spider;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class SpiderOrchestrationExtensionsTests
{
    [Fact]
    public void SpiderAdapterExposesExpectedPublicSurface()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Client.Spider",
            typeof(ClientSpiderMarker).Namespace);
    }

    [Fact]
    public void ResponseTransformOverloadKeepsTransformTopicVersionOrder()
    {
        var overload = typeof(SpiderOrchestrationExtensions)
            .GetMethods()
            .Where(method => method.Name == nameof(SpiderOrchestrationExtensions.Orchestrate))
            .Single(method =>
            {
                var parameters = method.GetParameters();
                return method.GetGenericArguments().Length == 2 &&
                    parameters.Length == 4 &&
                    parameters[1].ParameterType.Name.StartsWith("Func", StringComparison.Ordinal);
            });

        var names = overload.GetParameters().Select(parameter => parameter.Name).ToArray();

        Assert.Equal(["bridge", "transformPayload", "topic", "version"], names);
    }

    [Fact]
    public void RequestTransformOverloadKeepsTransformTopicVersionOrder()
    {
        var overload = typeof(SpiderOrchestrationExtensions)
            .GetMethods()
            .Where(method => method.Name == nameof(SpiderOrchestrationExtensions.Orchestrate))
            .Single(method =>
            {
                var parameters = method.GetParameters();
                return method.GetGenericArguments().Length == 1 &&
                    parameters.Length == 4 &&
                    parameters[1].ParameterType.Name.StartsWith("Func", StringComparison.Ordinal);
            });

        var names = overload.GetParameters().Select(parameter => parameter.Name).ToArray();

        Assert.Equal(["bridge", "transformPayload", "topic", "version"], names);
    }

    [Fact]
    public void TopicOverloadsUseSemanticVersion()
    {
        var versionParameters = typeof(SpiderOrchestrationExtensions)
            .GetMethods()
            .Where(method => method.Name == nameof(SpiderOrchestrationExtensions.Orchestrate))
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.Name == "version")
            .ToArray();

        Assert.NotEmpty(versionParameters);
        Assert.All(versionParameters, parameter =>
            Assert.Equal(typeof(OrchestrationSemanticVersion?), parameter.ParameterType));
    }

    private sealed record Request : IRequest;

    private sealed record ResponseRequest : IRequest<Response>;

    private sealed record Response;
}
