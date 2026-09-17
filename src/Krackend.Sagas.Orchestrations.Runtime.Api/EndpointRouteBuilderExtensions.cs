using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Maps optional REST API endpoints for runtime diagnostics and operations.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Runtime REST API using the default route prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapKrackendOrchestrationsRuntimeApi(this IEndpointRouteBuilder endpoints)
        => MapKrackendOrchestrationsRuntimeApi(endpoints, null);

    /// <summary>
    /// Maps the Runtime REST API.
    /// </summary>
    public static IEndpointRouteBuilder MapKrackendOrchestrationsRuntimeApi(
        this IEndpointRouteBuilder endpoints,
        Action<RuntimeRestApiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new RuntimeRestApiOptions();
        configure?.Invoke(options);

        var group = endpoints
            .MapGroup(NormalizePrefix(options.RoutePrefix))
            .WithTags("Krackend Runtime API");

        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        group.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        MapDiagnostics(group);
        MapArtifacts(group);
        MapIngresses(group, options);
        MapDesignNodes(group);
        MapManualPull(group);

        return endpoints;
    }

    private static void MapDiagnostics(RouteGroupBuilder group)
    {
        group.MapGet("/instances/snapshot", async (
            [FromServices] IRuntimeDiagnosticsReader diagnostics,
            CancellationToken cancellationToken)
            => Results.Ok(await diagnostics.GetSnapshot(cancellationToken)));

        group.MapGet("/instances/summary", async (
            [FromServices] IRuntimeDiagnosticsReader diagnostics,
            CancellationToken cancellationToken)
            => Results.Ok(await diagnostics.GetSummary(cancellationToken)));

        group.MapGet("/instances/{instanceId}", async (
            string instanceId,
            [FromServices] IRuntimeDiagnosticsReader diagnostics,
            CancellationToken cancellationToken)
            => string.IsNullOrWhiteSpace(instanceId)
                ? ApiEndpointResults.BadRequest("Instance id is required.")
                : Results.Ok(await diagnostics.GetDetail(instanceId, cancellationToken)));
    }

    private static void MapArtifacts(RouteGroupBuilder group)
    {
        group.MapGet("/artifacts", async (
            string search,
            [FromServices] IRuntimeArtifactRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                var artifacts = await repository.GetAll(cancellationToken);
                var rows = artifacts
                    .Where(x => MatchesArtifact(x, search))
                    .OrderByDescending(x => x.DeployedOnUtc)
                    .Select(RuntimeApiModelMapper.ToApiModel)
                    .ToArray();

                return Results.Ok(rows);
            });

        group.MapGet("/artifacts/{artifactId}", async (
            string artifactId,
            [FromServices] IRuntimeArtifactRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(artifactId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Artifact id is invalid.");
                }

                return Results.Ok(await repository.GetById(id, cancellationToken));
            });

        group.MapPost("/artifacts/{artifactId}/standup", async (
            string artifactId,
            [FromServices] IRuntimeArtifactRepository repository,
            [FromServices] IRuntimeArtifactReadyNotifier notifier,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(artifactId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Artifact id is invalid.");
                }

                var artifact = await repository.GetById(id, cancellationToken);
                if (artifact.Status != RuntimeOrchestrationArtifactStatus.Ready || !artifact.IsActive)
                {
                    return ApiEndpointResults.BadRequest("Only active ready artifacts can request ingress standup.");
                }

                await notifier.NotifyReadyAsync(RuntimeArtifactReadyGossipMessage.FromArtifact(artifact), cancellationToken);
                return Results.Accepted(value: new
                {
                    artifactId,
                    artifact.OrchestrationDefinitionKey,
                    version = artifact.Version.ToString(),
                    artifact.IngressGeneration
                });
            });
    }

    private static void MapIngresses(RouteGroupBuilder group, RuntimeRestApiOptions options)
    {
        group.MapGet("/ingresses", async (
            string artifactId,
            int? skip,
            int? take,
            [FromServices] IRuntimeIngressConfigurationRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                IReadOnlyCollection<RuntimeIngressConfiguration> configurations;
                if (!string.IsNullOrWhiteSpace(artifactId))
                {
                    if (!TryParseId(artifactId, out var id))
                    {
                        return ApiEndpointResults.BadRequest("Artifact id is invalid.");
                    }

                    configurations = await repository.GetActiveByArtifactIdAsync(id, cancellationToken);
                }
                else
                {
                    configurations = await repository.ReadActiveAsync(
                        Math.Max(0, skip ?? 0),
                        NormalizePageSize(options, take ?? 0),
                        cancellationToken);
                }

                return Results.Ok(configurations.Select(RuntimeApiModelMapper.ToApiModel).ToArray());
            });
    }

    private static void MapDesignNodes(RouteGroupBuilder group)
    {
        group.MapGet("/design-nodes", async (
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            => Results.Ok((await repository.GetAllAsync(cancellationToken))
                .Select(RuntimeApiModelMapper.ToApiModel)
                .ToArray()));

        group.MapGet("/design-nodes/{designNodeId}", async (
            string designNodeId,
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(designNodeId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Design node id is invalid.");
                }

                return Results.Ok(RuntimeApiModelMapper.ToApiModel(await repository.GetByIdAsync(id, cancellationToken)));
            });

        group.MapPost("/design-nodes", async (
            UpsertRuntimeDesignNodeRequest request,
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                var node = ToDesignNode(request);
                await repository.UpsertAsync(node, cancellationToken);
                return Results.Created(string.Empty, new { id = node.Id.ToString() });
            });

        group.MapPut("/design-nodes/{designNodeId}", async (
            string designNodeId,
            UpsertRuntimeDesignNodeRequest request,
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(designNodeId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Design node id is invalid.");
                }

                var node = ToDesignNode(request with { DesignNodeId = designNodeId });
                node.Id = id;
                await repository.UpsertAsync(node, cancellationToken);
                return Results.Ok(new { id = node.Id.ToString() });
            });

        group.MapPost("/design-nodes/{designNodeId}/status", async (
            string designNodeId,
            RuntimeDesignNodeStatusRequest request,
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(designNodeId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Design node id is invalid.");
                }

                await repository.SetStatusAsync(id, request.Status, cancellationToken);
                return Results.NoContent();
            });

        group.MapPost("/design-nodes/{designNodeId}/enabled", async (
            string designNodeId,
            RuntimeDesignNodeEnabledRequest request,
            [FromServices] IRuntimeDesignNodeRepository repository,
            CancellationToken cancellationToken)
            =>
            {
                if (!TryParseId(designNodeId, out var id))
                {
                    return ApiEndpointResults.BadRequest("Design node id is invalid.");
                }

                await repository.SetEnabledAsync(id, request.IsEnabled, cancellationToken);
                return Results.NoContent();
            });

        group.MapPost("/design-nodes/{designNodeId}/credentials/generate", async (
            string designNodeId,
            CredentialIssuerRequest request,
            [FromServices] IRuntimeDesignNodeConnectionService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GenerateCredentialPackageAsync(designNodeId, request.IssuerBaseUrl, cancellationToken)));

        group.MapPost("/design-nodes/credentials/import", async (
            ImportRuntimeDesignNodeCredentialPackageInput input,
            [FromServices] IRuntimeDesignNodeConnectionService service,
            CancellationToken cancellationToken)
            =>
            {
                await service.ImportCredentialPackageAsync(input, cancellationToken);
                return Results.NoContent();
            });

        group.MapPost("/design-nodes/{designNodeId}/validate", async (
            string designNodeId,
            [FromServices] IRuntimeDesignNodeConnectionService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.ValidateConnectionAsync(designNodeId, cancellationToken)));
    }

    private static void MapManualPull(RouteGroupBuilder group)
    {
        group.MapGet("/control-planes", ([FromServices] IControlPlaneArtifactPullService service)
            => Results.Ok(service.GetSources()));

        group.MapGet("/control-planes/{sourceKey}/artifacts/pending", async (
            string sourceKey,
            [FromServices] IControlPlaneArtifactPullService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetPendingAsync(sourceKey, cancellationToken)));

        group.MapPost("/control-planes/{sourceKey}/artifacts/{releaseTargetId}/apply", async (
            string sourceKey,
            string releaseTargetId,
            [FromServices] IControlPlaneArtifactPullService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.ApplyAsync(sourceKey, releaseTargetId, cancellationToken)));
    }

    private static RuntimeDesignNode ToDesignNode(UpsertRuntimeDesignNodeRequest request)
    {
        TryParseId(request.DesignNodeId, out var id);

        var now = DateTime.UtcNow;
        return new RuntimeDesignNode
        {
            Id = id == default ? Id.New() : id,
            Key = request.Key?.Trim() ?? string.Empty,
            Name = request.Name?.Trim() ?? string.Empty,
            EndpointBaseUri = request.EndpointBaseUri?.Trim().TrimEnd('/') ?? string.Empty,
            RemoteRuntimeNodeId = request.RemoteRuntimeNodeId?.Trim() ?? string.Empty,
            DistributionMode = request.DistributionMode,
            AccessTokenTtlSeconds = request.AccessTokenTtlSeconds <= 0 ? 86_400 : request.AccessTokenTtlSeconds,
            TokenRefreshSkewSeconds = request.TokenRefreshSkewSeconds <= 0 ? 300 : request.TokenRefreshSkewSeconds,
            TokenValidationCacheTtlSeconds = request.TokenValidationCacheTtlSeconds <= 0 ? 300 : request.TokenValidationCacheTtlSeconds,
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        };
    }

    private static bool MatchesArtifact(RuntimeOrchestrationArtifact artifact, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(artifact.Id.ToString(), search) ||
            Contains(artifact.OrchestrationDefinitionKey, search) ||
            Contains(artifact.Version.ToString(), search) ||
            Contains(artifact.ArtifactChecksum.Value, search);
    }

    private static bool Contains(string value, string search)
        => !string.IsNullOrWhiteSpace(value) &&
            value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryParseId(string value, out Id id)
    {
        if (Ulid.TryParse(value, out var ulid))
        {
            id = new Id(ulid);
            return true;
        }

        id = default;
        return false;
    }

    private static int NormalizePageSize(RuntimeRestApiOptions options, int take)
    {
        var requested = take <= 0 ? options.DefaultPageSize : take;
        return Math.Clamp(requested, 1, options.MaxPageSize);
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "/api/v1/runtime";
        }

        return "/" + prefix.Trim('/');
    }
}


