using Krackend.Security.Authorization;
using Krackend.Security.Core;
using Krackend.Security.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Krackend.Security.Api;

/// <summary>
/// Maps optional Krackend security administration endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Krackend security administration API using default options.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <returns>Endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapKrackendSecurityAdministrationApi(this IEndpointRouteBuilder endpoints)
        => endpoints.MapKrackendSecurityAdministrationApi(null);

    /// <summary>
    /// Maps the Krackend security administration API.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <param name="configure">Options callback.</param>
    /// <returns>Endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapKrackendSecurityAdministrationApi(
        this IEndpointRouteBuilder endpoints,
        Action<KrackendSecurityApiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new KrackendSecurityApiOptions();
        configure?.Invoke(options);

        var group = endpoints
            .MapGroup(NormalizePrefix(options.RoutePrefix))
            .WithTags("Krackend Security API");

        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        group.MapGet("/subjects", async (
            [FromServices] IKrackendSubjectRepository repository,
            int pageNumber,
            int pageSize,
            string searchText,
            CancellationToken cancellationToken)
            => Results.Ok(await repository.GetAll(
                SafePageNumber(pageNumber),
                SafePageSize(options, pageSize),
                searchText ?? string.Empty,
                cancellationToken)));

        group.MapPost("/subjects", async (
            UpsertKrackendSubjectRequest request,
            [FromServices] IKrackendSubjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var subject = ToSubject(request);
            await repository.Upsert(subject, cancellationToken);
            return Results.Created($"/subjects/{subject.Id}", subject);
        });

        group.MapGet("/subjects/{subjectId}", async (
            string subjectId,
            [FromServices] IKrackendSubjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var subject = await repository.GetById(subjectId, cancellationToken);
            return subject is null ? Results.NotFound() : Results.Ok(subject);
        });

        group.MapPatch("/subjects/{subjectId}/enabled", async (
            string subjectId,
            SetKrackendSubjectEnabledRequest request,
            [FromServices] IKrackendSubjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            await repository.SetEnabled(subjectId, request.IsEnabled, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/subjects/{subjectId}/roles", async (
            string subjectId,
            [FromServices] IKrackendSubjectRepository subjectRepository,
            [FromServices] IKrackendRoleAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            var subject = await subjectRepository.GetById(subjectId, cancellationToken);
            return subject is null ? Results.NotFound() : Results.Ok(await assignmentRepository.GetForSubject(subject, cancellationToken));
        });

        group.MapPost("/subjects/{subjectId}/roles", async (
            string subjectId,
            CreateKrackendRoleAssignmentRequest request,
            [FromServices] IKrackendSubjectRepository subjectRepository,
            [FromServices] IKrackendRoleAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            var subject = await subjectRepository.GetById(subjectId, cancellationToken);
            if (subject is null)
            {
                return Results.NotFound();
            }

            var assignment = ToRoleAssignment(subject, request);
            await assignmentRepository.Upsert(assignment, cancellationToken);
            return Results.Created($"/subjects/{subjectId}/roles/{assignment.Id}", assignment);
        });

        group.MapDelete("/subjects/{subjectId}/roles/{assignmentId}", async (
            string assignmentId,
            [FromServices] IKrackendRoleAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            await assignmentRepository.Remove(assignmentId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/subjects/{subjectId}/permissions", async (
            string subjectId,
            [FromServices] IKrackendSubjectRepository subjectRepository,
            [FromServices] IKrackendPermissionAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            var subject = await subjectRepository.GetById(subjectId, cancellationToken);
            return subject is null ? Results.NotFound() : Results.Ok(await assignmentRepository.GetForSubject(subject, cancellationToken));
        });

        group.MapPost("/subjects/{subjectId}/permissions", async (
            string subjectId,
            CreateKrackendPermissionAssignmentRequest request,
            [FromServices] IKrackendSubjectRepository subjectRepository,
            [FromServices] IKrackendPermissionAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            var subject = await subjectRepository.GetById(subjectId, cancellationToken);
            if (subject is null)
            {
                return Results.NotFound();
            }

            var assignment = ToPermissionAssignment(subject, request);
            await assignmentRepository.Upsert(assignment, cancellationToken);
            return Results.Created($"/subjects/{subjectId}/permissions/{assignment.Id}", assignment);
        });

        group.MapDelete("/subjects/{subjectId}/permissions/{assignmentId}", async (
            string assignmentId,
            [FromServices] IKrackendPermissionAssignmentRepository assignmentRepository,
            CancellationToken cancellationToken) =>
        {
            await assignmentRepository.Remove(assignmentId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/roles", () => Results.Ok(KrackendRoles.All));
        group.MapGet("/permissions", () => Results.Ok(KrackendPermissions.All));

        group.MapGet("/external-groups", async (
            [FromServices] IKrackendExternalGroupRoleAssignmentRepository repository,
            CancellationToken cancellationToken)
            => Results.Ok(await repository.GetAll(cancellationToken)));

        group.MapPost("/external-groups/{provider}/{groupId}/roles", async (
            string provider,
            string groupId,
            CreateKrackendExternalGroupRoleAssignmentRequest request,
            [FromServices] IKrackendExternalGroupRoleAssignmentRepository repository,
            CancellationToken cancellationToken) =>
        {
            var assignment = ToExternalGroupRoleAssignment(provider, groupId, request);
            await repository.Upsert(assignment, cancellationToken);
            return Results.Created($"/external-groups/{provider}/{groupId}/roles/{assignment.Id}", assignment);
        });

        group.MapDelete("/external-groups/{assignmentId}", async (
            string assignmentId,
            [FromServices] IKrackendExternalGroupRoleAssignmentRepository repository,
            CancellationToken cancellationToken) =>
        {
            await repository.Remove(assignmentId, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static KrackendSubject ToSubject(UpsertKrackendSubjectRequest request)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? NewId() : request.Id.Trim(),
            Provider = request.Provider?.Trim() ?? string.Empty,
            SubjectId = request.SubjectId?.Trim() ?? string.Empty,
            DisplayName = request.DisplayName?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            IsEnabled = request.IsEnabled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

    private static KrackendRoleAssignment ToRoleAssignment(KrackendSubject subject, CreateKrackendRoleAssignmentRequest request)
        => new()
        {
            Id = NewId(),
            Provider = subject.Provider,
            SubjectId = subject.SubjectId,
            Role = request.Role?.Trim() ?? string.Empty,
            ScopeType = NormalizeScopeType(request.ScopeType),
            ScopeId = request.ScopeId?.Trim() ?? string.Empty,
            Source = NormalizeSource(request.Source),
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

    private static KrackendPermissionAssignment ToPermissionAssignment(KrackendSubject subject, CreateKrackendPermissionAssignmentRequest request)
        => new()
        {
            Id = NewId(),
            Provider = subject.Provider,
            SubjectId = subject.SubjectId,
            Permission = request.Permission?.Trim() ?? string.Empty,
            ScopeType = NormalizeScopeType(request.ScopeType),
            ScopeId = request.ScopeId?.Trim() ?? string.Empty,
            Source = NormalizeSource(request.Source),
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

    private static KrackendExternalGroupRoleAssignment ToExternalGroupRoleAssignment(
        string provider,
        string groupId,
        CreateKrackendExternalGroupRoleAssignmentRequest request)
        => new()
        {
            Id = NewId(),
            Provider = provider?.Trim() ?? string.Empty,
            ExternalGroupId = groupId?.Trim() ?? string.Empty,
            Role = request.Role?.Trim() ?? string.Empty,
            ScopeType = NormalizeScopeType(request.ScopeType),
            ScopeId = request.ScopeId?.Trim() ?? string.Empty,
            Source = NormalizeSource(request.Source),
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

    private static int SafePageNumber(int pageNumber)
        => pageNumber <= 0 ? 1 : pageNumber;

    private static int SafePageSize(KrackendSecurityApiOptions options, int pageSize)
    {
        var value = pageSize <= 0 ? options.DefaultPageSize : pageSize;
        return Math.Min(Math.Max(value, 1), options.MaxPageSize);
    }

    private static string NormalizePrefix(string routePrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(routePrefix) ? "/api/v1/control-plane/security" : routePrefix.Trim();
        return prefix.StartsWith("/", StringComparison.Ordinal) ? prefix : "/" + prefix;
    }

    private static string NormalizeSource(string source)
        => string.IsNullOrWhiteSpace(source) ? KrackendAssignmentSources.Manual : source.Trim();

    private static string NormalizeScopeType(string scopeType)
        => string.IsNullOrWhiteSpace(scopeType) ? KrackendAuthorizationScopeTypes.Global : scopeType.Trim();

    private static string NewId()
        => Guid.NewGuid().ToString("N");
}
