using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.Web;

public static class RuntimeArtifactEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapKrackendSagasOrchestrationsArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/runtime/artifacts/deploy", async (
            RuntimeArtifactDeploymentRequest request,
            IRuntimeArtifactDeploymentService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.Deploy(request, cancellationToken);
            return result.Accepted
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        endpoints.MapGet("/runtime/artifacts/active/{orchestrationDefinitionKey}", async (
            string orchestrationDefinitionKey,
            IRuntimeArtifactDeploymentService service,
            CancellationToken cancellationToken) =>
        {
            var artifact = await service.GetActive(orchestrationDefinitionKey, cancellationToken);
            return artifact is null
                ? Results.NotFound()
                : Results.Ok(artifact);
        });

        endpoints.MapPost("/runtime/artifacts/pull", async (
            IRuntimeArtifactPullService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.PullPending(cancellationToken);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        });

        return endpoints;
    }
}
