using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public static class ArtifactDeliveryEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrchestratorArtifactDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/distribution/release-targets/{releaseTargetId}/push", async (
            string releaseTargetId,
            IArtifactDeliveryApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.Push(releaseTargetId, "api", cancellationToken);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        });

        endpoints.MapGet("/distribution/runtime-nodes/{runtimeNodeId}/artifacts/pending", async (
            string runtimeNodeId,
            IArtifactDeliveryApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var packages = await service.GetPendingForPull(runtimeNodeId, cancellationToken);
            return Results.Ok(packages);
        });

        endpoints.MapPost("/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack", async (
            string runtimeNodeId,
            string releaseTargetId,
            RuntimeArtifactPullAckRequest request,
            IArtifactDeliveryApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AcknowledgePull(
                runtimeNodeId,
                releaseTargetId,
                request?.RuntimeArtifactId ?? string.Empty,
                cancellationToken);

            return Results.Ok(result);
        });

        return endpoints;
    }
}
