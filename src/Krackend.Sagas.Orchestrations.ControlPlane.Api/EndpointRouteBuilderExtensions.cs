using System.Security.Claims;
using CPDesign = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using DesignPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings;
using CPDistribution = Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using DistributionPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution.ApplicationPagedSettings;
using CPSecurity = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using SecurityPagedSettings = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ApplicationPagedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Maps optional REST API endpoints for Control Plane design, distribution, and security workflows.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Control Plane REST API using the default route prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapKrackendOrchestrationsControlPlaneApi(this IEndpointRouteBuilder endpoints)
        => MapKrackendOrchestrationsControlPlaneApi(endpoints, null);

    /// <summary>
    /// Maps the Control Plane REST API.
    /// </summary>
    public static IEndpointRouteBuilder MapKrackendOrchestrationsControlPlaneApi(
        this IEndpointRouteBuilder endpoints,
        Action<ControlPlaneRestApiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new ControlPlaneRestApiOptions();
        configure?.Invoke(options);

        var group = endpoints
            .MapGroup(NormalizePrefix(options.RoutePrefix))
            .WithTags("Krackend Control Plane API");

        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        group.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        MapSecurity(group, options);
        MapDesign(group, options);
        MapDistribution(group, options);

        return endpoints;
    }

    private static void MapSecurity(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var manageGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.SecurityManagePolicy);

        readGroup.MapGet("/security/teams", async (
            [FromServices] CPSecurity.ITeamApplicationService service,
            int pageNumber,
            int pageSize,
            string searchText,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(
                new CPSecurity.GetTeamsQuery(
                    SecurityPaged(options, pageNumber, pageSize),
                    searchText ?? string.Empty),
                cancellationToken)));

        manageGroup.MapPost("/security/teams", async (
            CPSecurity.UpsertTeamCommand command,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Upsert(command, cancellationToken)));

        manageGroup.MapPut("/security/teams/{teamId}", async (
            string teamId,
            CPSecurity.UpsertTeamCommand command,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Upsert(
                command with { TeamId = string.IsNullOrWhiteSpace(command.TeamId) ? teamId : command.TeamId },
                cancellationToken)));

        manageGroup.MapPost("/security/teams/{teamId}/status", async (
            string teamId,
            SetActiveRequest request,
            ClaimsPrincipal user,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.SetIsActive(
                new CPSecurity.SetTeamIsActiveCommand(teamId, request.IsActive, NormalizeActor(request, user)),
                cancellationToken)));

        readGroup.MapGet("/security/teams/{teamId}/members", async (
            string teamId,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetMembers(new CPSecurity.GetTeamMembersQuery(teamId), cancellationToken)));

        manageGroup.MapPost("/security/teams/{teamId}/members", async (
            string teamId,
            AddTeamMemberRequest request,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.AddMember(
                new CPSecurity.AddTeamMemberCommand(teamId, request.ExternalUserId, request.DisplayName),
                cancellationToken)));

        manageGroup.MapDelete("/security/teams/{teamId}/members/{externalUserId}", async (
            string teamId,
            string externalUserId,
            [FromServices] CPSecurity.ITeamApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.NoContentOrNotFound(
                await service.RemoveMember(new CPSecurity.RemoveTeamMemberCommand(teamId, externalUserId), cancellationToken),
                "Team member was not removed."));
    }

    private static void MapDesign(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var writeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.DesignWritePolicy);

        readGroup.MapGet("/design/domains", async (
            [FromServices] CPDesign.IDomainApplicationService service,
            int pageNumber,
            int pageSize,
            string searchText,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(
                new CPDesign.GetDomainsQuery(DesignPaged(options, pageNumber, pageSize), searchText ?? string.Empty),
                cancellationToken)));

        readGroup.MapGet("/design/domains/{domainId}", async (
            string domainId,
            [FromServices] CPDesign.IDomainApplicationService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetDomainByIdQuery(domainId), cancellationToken)));

        writeGroup.MapPost("/design/domains", async (
            CPDesign.UpsertDomainCommand command,
            [FromServices] CPDesign.IDomainApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Upsert(command, cancellationToken)));

        writeGroup.MapPost("/design/domains/{domainId}/status", async (
            string domainId,
            SetActiveRequest request,
            [FromServices] CPDesign.IDomainApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.SetIsActive(
                new CPDesign.SetDomainIsActiveCommand(domainId, request.IsActive),
                cancellationToken)));

        readGroup.MapGet("/design/orchestrations", async (
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(
                new CPDesign.GetOrchestrationDefinitionsQuery(DesignPaged(options, pageNumber, pageSize)),
                cancellationToken)));

        readGroup.MapGet("/design/orchestrations/{orchestrationId}", async (
            string orchestrationId,
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetOrchestrationDefinitionByIdQuery(orchestrationId), cancellationToken)));

        writeGroup.MapPost("/design/orchestrations", async (
            CPDesign.CreateOrchestrationDefinitionCommand command,
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(command, cancellationToken)));

        writeGroup.MapPut("/design/orchestrations/{orchestrationId}", async (
            string orchestrationId,
            CPDesign.UpdateOrchestrationDefinitionCommand command,
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Update(
                command with { Id = string.IsNullOrWhiteSpace(command.Id) ? orchestrationId : command.Id },
                cancellationToken)));

        writeGroup.MapPost("/design/orchestrations/{orchestrationId}/activate", async (
            string orchestrationId,
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Activate(new CPDesign.ActivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken)));

        writeGroup.MapPost("/design/orchestrations/{orchestrationId}/deactivate", async (
            string orchestrationId,
            [FromServices] CPDesign.IOrchestrationApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Deactivate(new CPDesign.DeactivateOrchestrationDefinitionCommand(orchestrationId), cancellationToken)));

        readGroup.MapGet("/design/orchestrations/{orchestrationId}/versions", async (
            string orchestrationId,
            [FromServices] CPDesign.IOrchestrationVersionApplicationService service,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(
                new CPDesign.GetOrchestrationVersionsQuery(orchestrationId, DesignPaged(options, pageNumber, pageSize)),
                cancellationToken)));

        writeGroup.MapPost("/design/orchestrations/{orchestrationId}/versions", async (
            string orchestrationId,
            CPDesign.CreateOrchestrationVersionCommand command,
            [FromServices] CPDesign.IOrchestrationVersionApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(
                command with { OrchestrationDefinitionId = string.IsNullOrWhiteSpace(command.OrchestrationDefinitionId) ? orchestrationId : command.OrchestrationDefinitionId },
                cancellationToken)));

        readGroup.MapGet("/design/versions/{versionId}", async (
            string versionId,
            [FromServices] CPDesign.IOrchestrationVersionApplicationService service,
            CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetOrchestrationVersionByIdQuery(versionId), cancellationToken)));

        writeGroup.MapPut("/design/versions/{versionId}", async (
            string versionId,
            CPDesign.UpdateOrchestrationVersionCommand command,
            [FromServices] CPDesign.IOrchestrationVersionApplicationService service,
            CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Update(
                command with { Id = string.IsNullOrWhiteSpace(command.Id) ? versionId : command.Id },
                cancellationToken)));

        MapVersionActions(group, options);
        MapStageEndpoints(group, options);
        MapTriggerEndpoints(group, options);
        MapTaskEndpoints(group, options);
    }

    private static void MapVersionActions(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var writeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.DesignWritePolicy);
        var releaseGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReleaseExecutePolicy);

        writeGroup.MapPost("/design/versions/{versionId}/submit-review", async (string versionId, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.SetInReview(new CPDesign.SetOrchestrationVersionInReviewCommand(versionId), cancellationToken)));
        writeGroup.MapPost("/design/versions/{versionId}/return-draft", async (string versionId, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.ReturnToDraft(new CPDesign.ReturnOrchestrationVersionToDraftCommand(versionId), cancellationToken)));
        writeGroup.MapPost("/design/versions/{versionId}/reopen-review", async (string versionId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.ReopenReview(new CPDesign.ReopenOrchestrationVersionReviewCommand(versionId, NormalizeActor(request, user)), cancellationToken)));
        releaseGroup.MapPost("/design/versions/{versionId}/approve", async (string versionId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Approve(new CPDesign.ApproveOrchestrationVersionCommand(versionId, NormalizeActor(request, user)), cancellationToken)));
        releaseGroup.MapPost("/design/versions/{versionId}/deploy", async (string versionId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Deploy(new CPDesign.DeployOrchestrationVersionCommand(versionId, NormalizeActor(request, user)), cancellationToken)));
        releaseGroup.MapPost("/design/versions/{versionId}/deprecate", async (string versionId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Deprecate(new CPDesign.DeprecateOrchestrationVersionCommand(versionId, NormalizeActor(request, user)), cancellationToken)));
        releaseGroup.MapPost("/design/versions/{versionId}/archive", async (string versionId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDesign.IOrchestrationVersionApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Archive(new CPDesign.ArchiveOrchestrationVersionCommand(versionId, NormalizeActor(request, user)), cancellationToken)));
    }

    private static void MapStageEndpoints(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var writeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.DesignWritePolicy);

        readGroup.MapGet("/design/versions/{versionId}/stages", async (string versionId, [FromServices] CPDesign.IStageApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(new CPDesign.GetStageDefinitionsQuery(versionId), cancellationToken)));
        writeGroup.MapPost("/design/versions/{versionId}/stages", async (string versionId, CPDesign.CreateStageDefinitionCommand command, [FromServices] CPDesign.IStageApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(command with { OrchestrationVersionId = string.IsNullOrWhiteSpace(command.OrchestrationVersionId) ? versionId : command.OrchestrationVersionId }, cancellationToken)));
        readGroup.MapGet("/design/stages/{stageId}", async (string stageId, [FromServices] CPDesign.IStageApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetStageDefinitionByIdQuery(stageId), cancellationToken)));
        writeGroup.MapPut("/design/stages/{stageId}", async (string stageId, CPDesign.UpdateStageDefinitionCommand command, [FromServices] CPDesign.IStageApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Update(command with { Id = string.IsNullOrWhiteSpace(command.Id) ? stageId : command.Id }, cancellationToken)));
        writeGroup.MapDelete("/design/stages/{stageId}", async (string stageId, [FromServices] CPDesign.IStageApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.NoContentOrNotFound(await service.Delete(new CPDesign.DeleteStageDefinitionCommand(stageId), cancellationToken), "Stage was not deleted."));
    }

    private static void MapTriggerEndpoints(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var writeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.DesignWritePolicy);

        readGroup.MapGet("/design/versions/{versionId}/triggers", async (string versionId, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(new CPDesign.GetTriggerBindingsQuery(versionId), cancellationToken)));
        writeGroup.MapPost("/design/versions/{versionId}/triggers", async (string versionId, CPDesign.CreateTriggerBindingCommand command, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(command with { OrchestrationVersionId = string.IsNullOrWhiteSpace(command.OrchestrationVersionId) ? versionId : command.OrchestrationVersionId }, cancellationToken)));
        readGroup.MapGet("/design/triggers/{triggerId}", async (string triggerId, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetTriggerBindingByIdQuery(triggerId), cancellationToken)));
        writeGroup.MapPut("/design/triggers/{triggerId}", async (string triggerId, CPDesign.UpdateTriggerBindingCommand command, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Update(command with { Id = string.IsNullOrWhiteSpace(command.Id) ? triggerId : command.Id }, cancellationToken)));
        writeGroup.MapPost("/design/triggers/{triggerId}/enable", async (string triggerId, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Enable(new CPDesign.EnableTriggerBindingCommand(triggerId), cancellationToken)));
        writeGroup.MapPost("/design/triggers/{triggerId}/disable", async (string triggerId, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Disable(new CPDesign.DisableTriggerBindingCommand(triggerId), cancellationToken)));
        writeGroup.MapDelete("/design/triggers/{triggerId}", async (string triggerId, [FromServices] CPDesign.ITriggerBindingApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.NoContentOrNotFound(await service.Delete(new CPDesign.DeleteTriggerBindingCommand(triggerId), cancellationToken), "Trigger was not deleted."));
    }

    private static void MapTaskEndpoints(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var writeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.DesignWritePolicy);

        readGroup.MapGet("/design/stages/{stageId}/tasks", async (string stageId, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(new CPDesign.GetTaskDefinitionsQuery(stageId), cancellationToken)));
        writeGroup.MapPost("/design/stages/{stageId}/tasks", async (string stageId, CPDesign.CreateTaskDefinitionCommand command, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(command with { StageDefinitionId = string.IsNullOrWhiteSpace(command.StageDefinitionId) ? stageId : command.StageDefinitionId }, cancellationToken)));
        readGroup.MapGet("/design/tasks/{taskId}", async (string taskId, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetById(new CPDesign.GetTaskDefinitionByIdQuery(taskId), cancellationToken)));
        writeGroup.MapPut("/design/tasks/{taskId}", async (string taskId, CPDesign.UpdateTaskDefinitionCommand command, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Update(command with { Id = string.IsNullOrWhiteSpace(command.Id) ? taskId : command.Id }, cancellationToken)));
        writeGroup.MapPost("/design/tasks/{taskId}/enable", async (string taskId, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Enable(new CPDesign.EnableTaskDefinitionCommand(taskId), cancellationToken)));
        writeGroup.MapPost("/design/tasks/{taskId}/disable", async (string taskId, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Ok(await service.Disable(new CPDesign.DisableTaskDefinitionCommand(taskId), cancellationToken)));
        writeGroup.MapDelete("/design/tasks/{taskId}", async (string taskId, [FromServices] CPDesign.ITaskApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.NoContentOrNotFound(await service.Delete(new CPDesign.DeleteTaskDefinitionCommand(taskId), cancellationToken), "Task was not deleted."));
        readGroup.MapGet("/design/versions/{versionId}/tasks/{taskId}/schema-context", async (string versionId, string taskId, [FromServices] CPDesign.IOrchestrationSchemaContextApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetForTask(new CPDesign.GetTaskSchemaContextQuery(versionId, taskId), cancellationToken)));
    }

    private static void MapDistribution(RouteGroupBuilder group, ControlPlaneRestApiOptions options)
    {
        var readGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReadPolicy);
        var executeGroup = RequirePolicy(group.MapGroup(string.Empty), options.Authorization.ReleaseExecutePolicy);

        readGroup.MapGet("/distribution/runtime-nodes", async ([FromServices] CPDistribution.IRuntimeNodeApplicationService service, int pageNumber, int pageSize, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(DistributionPaged(options, pageNumber, pageSize), cancellationToken)));
        executeGroup.MapPost("/distribution/runtime-nodes", async (CPDistribution.UpsertRuntimeNodeInput input, [FromServices] CPDistribution.IRuntimeNodeApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Upsert(input, cancellationToken)));
        executeGroup.MapPut("/distribution/runtime-nodes/{runtimeNodeId}", async (string runtimeNodeId, CPDistribution.UpsertRuntimeNodeInput input, [FromServices] CPDistribution.IRuntimeNodeApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Upsert(input with { RuntimeNodeId = string.IsNullOrWhiteSpace(input.RuntimeNodeId) ? runtimeNodeId : input.RuntimeNodeId }, cancellationToken)));
        executeGroup.MapPost("/distribution/runtime-nodes/{runtimeNodeId}/status", async (string runtimeNodeId, [FromBody] RuntimeNodeStatusRequest request, [FromServices] CPDistribution.IRuntimeNodeApplicationService service, CancellationToken cancellationToken)
            =>
            {
                if (request is null)
                {
                    return ApiEndpointResults.BadRequest("Runtime node status request is required.");
                }

                await service.SetStatus(runtimeNodeId, request.Status, cancellationToken);
                return Results.NoContent();
            });
        executeGroup.MapDelete("/distribution/runtime-nodes/{runtimeNodeId}", async (string runtimeNodeId, [FromServices] CPDistribution.IRuntimeNodeApplicationService service, CancellationToken cancellationToken)
            => { await service.Delete(runtimeNodeId, cancellationToken); return Results.NoContent(); });
        executeGroup.MapPost("/distribution/runtime-nodes/{runtimeNodeId}/credentials/generate", async (string runtimeNodeId, CredentialIssuerRequest request, [FromServices] CPDistribution.IRuntimeNodeConnectionApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GenerateCredentialPackage(runtimeNodeId, request.IssuerBaseUrl, cancellationToken)));
        executeGroup.MapPost("/distribution/runtime-nodes/credentials/import", async (CPDistribution.ImportRuntimeNodeCredentialPackageInput input, [FromServices] CPDistribution.IRuntimeNodeConnectionApplicationService service, CancellationToken cancellationToken)
            => { await service.ImportCredentialPackage(input, cancellationToken); return Results.NoContent(); });
        executeGroup.MapPost("/distribution/runtime-nodes/{runtimeNodeId}/validate", async (string runtimeNodeId, [FromServices] CPDistribution.IRuntimeNodeConnectionApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.ValidateConnection(runtimeNodeId, cancellationToken)));

        readGroup.MapGet("/distribution/artifacts", async ([FromServices] CPDistribution.IArtifactApplicationService service, int pageNumber, int pageSize, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(DistributionPaged(options, pageNumber, pageSize), cancellationToken)));
        readGroup.MapGet("/distribution/releases", async ([FromServices] CPDistribution.IReleaseApplicationService service, int pageNumber, int pageSize, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(DistributionPaged(options, pageNumber, pageSize), cancellationToken)));
        executeGroup.MapPost("/distribution/releases", async (CPDistribution.CreateReleaseInput input, [FromServices] CPDistribution.IReleaseApplicationService service, CancellationToken cancellationToken)
            => ApiEndpointResults.Created(await service.Create(input, cancellationToken)));
        readGroup.MapGet("/distribution/release-targets", async ([FromServices] CPDistribution.IReleaseTargetApplicationService service, int pageNumber, int pageSize, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAll(DistributionPaged(options, pageNumber, pageSize), cancellationToken)));
        readGroup.MapGet("/distribution/release-targets/{releaseTargetId}/attempts", async (string releaseTargetId, [FromServices] CPDistribution.IReleaseTargetApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAttempts(releaseTargetId, cancellationToken)));
        executeGroup.MapPost("/distribution/release-targets/{releaseTargetId}/push", async (string releaseTargetId, [FromBody] ActorRequest request, ClaimsPrincipal user, [FromServices] CPDistribution.IArtifactDeliveryApplicationService service, CancellationToken cancellationToken)
            => Results.Ok(await service.Push(releaseTargetId, NormalizeActor(request, user), cancellationToken)));
    }

    private static DesignPagedSettings DesignPaged(ControlPlaneRestApiOptions options, int pageNumber, int pageSize)
        => new()
        {
            PageNumber = NormalizePageNumber(pageNumber),
            PageSize = NormalizePageSize(options, pageSize)
        };

    private static DistributionPagedSettings DistributionPaged(ControlPlaneRestApiOptions options, int pageNumber, int pageSize)
        => new()
        {
            PageNumber = NormalizePageNumber(pageNumber),
            PageSize = NormalizePageSize(options, pageSize)
        };

    private static SecurityPagedSettings SecurityPaged(ControlPlaneRestApiOptions options, int pageNumber, int pageSize)
        => new()
        {
            PageNumber = NormalizePageNumber(pageNumber),
            PageSize = NormalizePageSize(options, pageSize)
        };

    private static int NormalizePageNumber(int pageNumber) => pageNumber <= 0 ? 1 : pageNumber;

    private static string NormalizeActor(ActorRequest request)
        => string.IsNullOrWhiteSpace(request?.Actor) ? "api" : request.Actor;

    private static string NormalizeActor(SetActiveRequest request, ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated == true)
        {
            var actor =
                user.FindFirst("oid")?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                user.FindFirst("sub")?.Value ??
                user.Identity.Name;

            if (!string.IsNullOrWhiteSpace(actor))
            {
                return actor;
            }
        }

        return string.IsNullOrWhiteSpace(request?.Actor) ? "api" : request.Actor;
    }

    private static string NormalizeActor(ActorRequest request, ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated == true)
        {
            var actor =
                user.FindFirst("oid")?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                user.FindFirst("sub")?.Value ??
                user.Identity.Name;

            if (!string.IsNullOrWhiteSpace(actor))
            {
                return actor;
            }
        }

        return NormalizeActor(request);
    }

    private static int NormalizePageSize(ControlPlaneRestApiOptions options, int pageSize)
    {
        var requested = pageSize <= 0 ? options.DefaultPageSize : pageSize;
        return Math.Clamp(requested, 1, options.MaxPageSize);
    }

    private static RouteGroupBuilder RequirePolicy(RouteGroupBuilder group, string policy)
    {
        if (!string.IsNullOrWhiteSpace(policy))
        {
            group.RequireAuthorization(policy);
        }

        return group;
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "/api/v1/control-plane";
        }

        return "/" + prefix.Trim('/');
    }
}



