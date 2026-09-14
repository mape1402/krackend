using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public static class ArtifactDeliveryEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrchestratorArtifactDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/distribution/connect/token", async (
            ConnectionTokenRequest request,
            IControlPlaneConnectionTokenIssuer tokenIssuer,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var token = await tokenIssuer.IssueAsync(request, cancellationToken);
                return Results.Ok(token);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "invalid_client", error_description = ex.Message });
            }
        });

        endpoints.MapGet("/distribution/runtime-nodes/{runtimeNodeId}/connect/validate", async (
            string runtimeNodeId,
            HttpContext httpContext,
            IControlPlaneConnectionTokenValidator tokenValidator,
            CancellationToken cancellationToken) =>
        {
            var validation = await tokenValidator.ValidateAsync(
                ReadBearerToken(httpContext.Request),
                runtimeNodeId,
                [ArtifactDeliveryScope.ConnectionValidate],
                cancellationToken);

            return validation.Succeeded
                ? Results.Ok(new { succeeded = true, runtimeNodeId, nodeKey = validation.Principal.NodeKey })
                : Results.Unauthorized();
        });

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
            HttpContext httpContext,
            IArtifactDeliveryApplicationService service,
            IArtifactDeliveryEndpointAuthenticator authenticator,
            CancellationToken cancellationToken) =>
        {
            var authentication = await authenticator.AuthenticateRuntimeNodeAsync(
                httpContext.Request,
                runtimeNodeId,
                string.Empty,
                cancellationToken);
            if (!authentication.Succeeded)
            {
                return Results.Unauthorized();
            }

            var packages = await service.GetPendingForPull(runtimeNodeId, cancellationToken);
            return Results.Ok(packages);
        });

        endpoints.MapGet("/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}", async (
            string runtimeNodeId,
            string releaseTargetId,
            HttpContext httpContext,
            IArtifactDeliveryApplicationService service,
            IArtifactDeliveryEndpointAuthenticator authenticator,
            CancellationToken cancellationToken) =>
        {
            var authentication = await authenticator.AuthenticateRuntimeNodeAsync(
                httpContext.Request,
                runtimeNodeId,
                string.Empty,
                cancellationToken);
            if (!authentication.Succeeded)
            {
                return Results.Unauthorized();
            }

            var package = await service.GetForPull(runtimeNodeId, releaseTargetId, cancellationToken);
            return Results.Ok(package);
        });

        endpoints.MapPost("/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack", async (
            string runtimeNodeId,
            string releaseTargetId,
            HttpContext httpContext,
            IArtifactDeliveryApplicationService service,
            IArtifactDeliveryEndpointAuthenticator authenticator,
            CancellationToken cancellationToken) =>
        {
            var body = await ReadBody(httpContext.Request);
            var authentication = await authenticator.AuthenticateRuntimeNodeAsync(
                httpContext.Request,
                runtimeNodeId,
                body,
                cancellationToken);
            if (!authentication.Succeeded)
            {
                return Results.Unauthorized();
            }

            var request = JsonSerializer.Deserialize<RuntimeArtifactPullAckRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var result = await service.AcknowledgePull(
                runtimeNodeId,
                releaseTargetId,
                request?.RuntimeArtifactId ?? string.Empty,
                request?.RuntimeArtifactStatus ?? string.Empty,
                cancellationToken);

            return Results.Ok(result);
        });

        return endpoints;
    }

    private static async Task<string> ReadBody(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }

    private static string ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
