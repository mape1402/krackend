using Krackend.Security.AspNetCore;
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

        app.MapKrackendOrchestrationsControlPlaneApi(options => options.Authorization.UseKrackendDefaults());

        AssertEndpointPolicy(app, "/api/v1/control-plane/design/orchestrations", "GET", KrackendAuthorizationPolicies.ControlPlaneRead);
        AssertEndpointPolicy(app, "/api/v1/control-plane/design/orchestrations", "POST", KrackendAuthorizationPolicies.ControlPlaneDesignWrite);
        AssertEndpointPolicy(app, "/api/v1/control-plane/design/versions/{versionId}/deploy", "POST", KrackendAuthorizationPolicies.ControlPlaneReleaseExecute);
        AssertEndpointPolicy(app, "/api/v1/control-plane/security/teams", "POST", KrackendAuthorizationPolicies.ControlPlaneSecurityManage);
    }

    [Fact]
    public void RuntimeApiAppliesGranularAuthorizationMetadata()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        var app = builder.Build();

        app.MapKrackendOrchestrationsRuntimeApi(options => options.Authorization.UseKrackendDefaults());

        AssertEndpointPolicy(app, "/api/v1/runtime/instances/summary", "GET", KrackendAuthorizationPolicies.RuntimeInstancesRead);
        AssertEndpointPolicy(app, "/api/v1/runtime/artifacts", "GET", KrackendAuthorizationPolicies.RuntimeRead);
        AssertEndpointPolicy(app, "/api/v1/runtime/artifacts/{artifactId}/standup", "POST", KrackendAuthorizationPolicies.RuntimeArtifactsApply);
        AssertEndpointPolicy(app, "/api/v1/runtime/design-nodes", "POST", KrackendAuthorizationPolicies.RuntimeManage);
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
