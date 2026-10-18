using Krackend.Sagas.Orchestrations.ControlPlane.Api;
using Krackend.Sagas.Orchestrations.Runtime.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class RestApiAuthorizationMetadataTests
{
    [Fact]
    public void ControlPlaneApiAppliesGranularAuthorizationMetadata()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        var app = builder.Build();

        app.MapKrackendOrchestrationsControlPlaneApi(options =>
        {
            options.Authorization.ReadPolicy = "control-plane-read";
            options.Authorization.DesignWritePolicy = "control-plane-design-write";
            options.Authorization.ReleaseExecutePolicy = "control-plane-release-execute";
            options.Authorization.SecurityManagePolicy = "control-plane-security-manage";
        });

        AssertEndpointPolicy(app, "/api/v1/control-plane/design/orchestrations", "GET", "control-plane-read");
        AssertEndpointPolicy(app, "/api/v1/control-plane/design/orchestrations", "POST", "control-plane-design-write");
        AssertEndpointPolicy(app, "/api/v1/control-plane/design/reviews/{reviewId}/apply", "POST", "control-plane-release-execute");
        AssertEndpointPolicy(app, "/api/v1/control-plane/security/teams", "POST", "control-plane-security-manage");
    }

    [Fact]
    public void RuntimeApiAppliesGranularAuthorizationMetadata()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        var app = builder.Build();

        app.MapKrackendOrchestrationsRuntimeApi(options =>
        {
            options.Authorization.InstancesReadPolicy = "runtime-instances-read";
            options.Authorization.ReadPolicy = "runtime-read";
            options.Authorization.ArtifactApplyPolicy = "runtime-artifacts-apply";
            options.Authorization.ManagePolicy = "runtime-manage";
        });

        AssertEndpointPolicy(app, "/api/v1/runtime/instances/summary", "GET", "runtime-instances-read");
        AssertEndpointPolicy(app, "/api/v1/runtime/artifacts", "GET", "runtime-read");
        AssertEndpointPolicy(app, "/api/v1/runtime/artifacts/{artifactId}/standup", "POST", "runtime-artifacts-apply");
        AssertEndpointPolicy(app, "/api/v1/runtime/design-nodes", "POST", "runtime-manage");
    }

    private static void AssertEndpointPolicy(WebApplication app, string pattern, string method, string expectedPolicy)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .First(x =>
                string.Equals(x.RoutePattern.RawText, pattern, StringComparison.OrdinalIgnoreCase) &&
                x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true);

        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy).ToArray();
        Assert.Contains(expectedPolicy, policies);
    }
}
