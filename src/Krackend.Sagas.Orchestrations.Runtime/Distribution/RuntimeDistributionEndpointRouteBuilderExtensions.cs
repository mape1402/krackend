using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Maps runtime artifact distribution endpoints.
/// </summary>
public static class RuntimeDistributionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps push and manual pull endpoints for runtime artifact distribution.
    /// </summary>
    public static IEndpointRouteBuilder MapOrchestratorRuntimeDistributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/runtime/distribution/connect/token", async (
            ConnectionTokenRequest request,
            IRuntimeConnectionTokenIssuer tokenIssuer,
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

        endpoints.MapGet("/runtime/distribution/connect/validate", async (
            HttpContext httpContext,
            IRuntimeConnectionTokenValidator tokenValidator,
            CancellationToken cancellationToken) =>
        {
            var validation = await tokenValidator.ValidateAsync(
                ReadBearerToken(httpContext.Request),
                [ArtifactDeliveryScope.ConnectionValidate],
                cancellationToken);

            return validation.Succeeded
                ? Results.Ok(new { succeeded = true, nodeKey = validation.Principal.NodeKey })
                : Results.Unauthorized();
        });

        endpoints.MapPost("/runtime/artifacts/deploy", async (
            HttpContext httpContext,
            IRuntimeArtifactDeliveryEndpointAuthenticator authenticator,
            IRuntimeArtifactDeploymentService deploymentService,
            CancellationToken cancellationToken) =>
        {
            var body = await ReadBody(httpContext.Request);
            var authentication = await authenticator.AuthenticateAsync(httpContext.Request, body, cancellationToken);
            if (!authentication.Succeeded)
            {
                return Results.Unauthorized();
            }

            var package = JsonSerializer.Deserialize<RuntimeArtifactDeliveryPackage>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (package is null)
            {
                return Results.BadRequest(new RuntimeArtifactDeploymentResult
                {
                    Accepted = false,
                    Status = "Rejected",
                    Message = "Artifact package body is empty."
                });
            }

            var result = await deploymentService.DeployAsync(package, authentication.SourceKey, cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapGet("/runtime/distribution/control-planes", (
            IControlPlaneArtifactPullService service) =>
        {
            var sources = service.GetSources();
            return Results.Ok(sources);
        });

        endpoints.MapGet("/runtime/distribution/control-planes/{sourceKey}/artifacts/pending", async (
            string sourceKey,
            IControlPlaneArtifactPullService service,
            CancellationToken cancellationToken) =>
        {
            var packages = await service.GetPendingAsync(sourceKey, cancellationToken);
            return Results.Ok(packages);
        });

        endpoints.MapPost("/runtime/distribution/control-planes/{sourceKey}/artifacts/{releaseTargetId}/apply", async (
            string sourceKey,
            string releaseTargetId,
            IControlPlaneArtifactPullService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApplyAsync(sourceKey, releaseTargetId, cancellationToken);
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
