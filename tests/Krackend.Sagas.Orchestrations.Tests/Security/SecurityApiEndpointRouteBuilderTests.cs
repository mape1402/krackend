using System.Text;
using System.Text.Json;
using System.Reflection;
using Krackend.Sagas.Orchestrations.Security.Api;
using Krackend.Sagas.Orchestrations.Security.Authorization;
using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class SecurityApiEndpointRouteBuilderTests
{
    [Fact]
    public async Task SecurityApiMapsSubjectLifecycleAndLookupEndpoints()
    {
        var subjectRepository = Substitute.For<IKrackendSubjectRepository>();
        var subject = CreateSubject("subject-1");
        KrackendSubject? capturedSubject = null;

        subjectRepository.GetAll(1, 9, "alice", Arg.Any<CancellationToken>())
            .Returns(new KrackendPagedResult<KrackendSubject>(1, 1, 1, 9, [subject]));
        subjectRepository.GetById("subject-1", Arg.Any<CancellationToken>()).Returns(subject);
        subjectRepository.GetById("missing", Arg.Any<CancellationToken>()).Returns((KrackendSubject)null!);
        subjectRepository.Upsert(Arg.Do<KrackendSubject>(x => capturedSubject = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var app = BuildApp(
            services => services.AddSingleton(subjectRepository),
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.RoutePrefix = "security";
                options.AuthorizationPolicy = string.Empty;
                options.DefaultPageSize = 7;
                options.MaxPageSize = 9;
            }));

        var list = await Invoke(app, "GET", "/security/subjects", queryString: "?pageNumber=-2&pageSize=99&searchText=alice");
        var created = await Invoke(
            app,
            "POST",
            "/security/subjects",
            JsonSerializer.Serialize(new UpsertKrackendSubjectRequest
            {
                Id = " subject-2 ",
                Provider = " entra ",
                SubjectId = " alice ",
                DisplayName = " Alice Example ",
                Email = " alice@example.test ",
                IsEnabled = false
            }));
        var found = await Invoke(
            app,
            "GET",
            "/security/subjects/{subjectId}",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var missing = await Invoke(
            app,
            "GET",
            "/security/subjects/{subjectId}",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "missing" });
        var enabled = await Invoke(
            app,
            "PATCH",
            "/security/subjects/{subjectId}/enabled",
            JsonSerializer.Serialize(new SetKrackendSubjectEnabledRequest { IsEnabled = true }),
            new Dictionary<string, object?> { ["subjectId"] = "subject-2" });
        var roles = await Invoke(app, "GET", "/security/roles");
        var permissions = await Invoke(app, "GET", "/security/permissions");

        Assert.Equal(StatusCodes.Status200OK, list.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, found.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, enabled.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, roles.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, permissions.StatusCode);
        Assert.Contains("Alice Example", list.Body, StringComparison.Ordinal);
        Assert.Contains(KrackendRoles.Admin, roles.Body, StringComparison.Ordinal);
        Assert.Contains(KrackendPermissions.ControlPlaneRead, permissions.Body, StringComparison.Ordinal);
        Assert.NotNull(capturedSubject);
        Assert.Equal("subject-2", capturedSubject.Id);
        Assert.Equal("entra", capturedSubject.Provider);
        Assert.Equal("alice", capturedSubject.SubjectId);
        Assert.Equal("Alice Example", capturedSubject.DisplayName);
        Assert.Equal("alice@example.test", capturedSubject.Email);
        Assert.False(capturedSubject.IsEnabled);

        await subjectRepository.Received(1).GetAll(1, 9, "alice", Arg.Any<CancellationToken>());
        await subjectRepository.Received(1).SetEnabled("subject-2", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecurityApiUsesDefaultPrefixAuthorizationPolicyAndSubjectFallbacks()
    {
        var subjectRepository = Substitute.For<IKrackendSubjectRepository>();
        KrackendSubject? capturedSubject = null;
        subjectRepository.GetAll(2, 5, string.Empty, Arg.Any<CancellationToken>())
            .Returns(new KrackendPagedResult<KrackendSubject>(2, 5, 0, 5, []));
        subjectRepository.Upsert(Arg.Do<KrackendSubject>(x => capturedSubject = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var app = BuildApp(
            services => services.AddSingleton(subjectRepository),
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.AuthorizationPolicy = "security-admin";
                options.DefaultPageSize = 5;
                options.MaxPageSize = 10;
            }));

        var subjectEndpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x => string.Equals(x.RoutePattern.RawText, "/api/v1/control-plane/security/subjects", StringComparison.Ordinal) &&
                x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) == true);
        var policyNames = subjectEndpoint.Metadata
            .OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Select(x => x.Policy)
            .ToArray();

        var listed = await Invoke(app, "GET", "/api/v1/control-plane/security/subjects", queryString: "?pageNumber=2&pageSize=0");
        var created = await Invoke(
            app,
            "POST",
            "/api/v1/control-plane/security/subjects",
            JsonSerializer.Serialize(new UpsertKrackendSubjectRequest
            {
                Id = " ",
                Provider = null,
                SubjectId = null,
                DisplayName = null,
                Email = null,
                IsEnabled = true
            }));

        Assert.Contains("security-admin", policyNames);
        Assert.Equal(StatusCodes.Status200OK, listed.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.NotNull(capturedSubject);
        Assert.False(string.IsNullOrWhiteSpace(capturedSubject!.Id));
        Assert.Equal(string.Empty, capturedSubject.Provider);
        Assert.Equal(string.Empty, capturedSubject.SubjectId);
        Assert.Equal(string.Empty, capturedSubject.DisplayName);
        Assert.Equal(string.Empty, capturedSubject.Email);
        Assert.True(capturedSubject.IsEnabled);
        await subjectRepository.Received(1).GetAll(2, 5, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SecurityApiRejectsNullEndpointRouteBuilder()
        => Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Security.Api.EndpointRouteBuilderExtensions.MapKrackendSecurityAdministrationApi(null!));

    [Fact]
    public async Task SecurityApiWhitespacePrefixFallsBackToDefaultRoute()
    {
        var subjectRepository = Substitute.For<IKrackendSubjectRepository>();
        subjectRepository.GetAll(1, 25, string.Empty, Arg.Any<CancellationToken>())
            .Returns(new KrackendPagedResult<KrackendSubject>(1, 25, 0, 25, []));

        await using var app = BuildApp(
            services => services.AddSingleton(subjectRepository),
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.RoutePrefix = " ";
                options.AuthorizationPolicy = string.Empty;
            }));

        var listed = await Invoke(app, "GET", "/api/v1/control-plane/security/subjects", queryString: "?pageNumber=1&pageSize=25");

        Assert.Equal(StatusCodes.Status200OK, listed.StatusCode);
        await subjectRepository.Received(1).GetAll(1, 25, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecurityApiMapsDirectRoleAndPermissionAssignments()
    {
        var subjectRepository = Substitute.For<IKrackendSubjectRepository>();
        var roleRepository = Substitute.For<IKrackendRoleAssignmentRepository>();
        var permissionRepository = Substitute.For<IKrackendPermissionAssignmentRepository>();
        var subject = CreateSubject("subject-1");
        KrackendRoleAssignment? capturedRole = null;
        KrackendPermissionAssignment? capturedPermission = null;

        subjectRepository.GetById("subject-1", Arg.Any<CancellationToken>()).Returns(subject);
        subjectRepository.GetById("missing", Arg.Any<CancellationToken>()).Returns((KrackendSubject)null!);
        roleRepository.GetForSubject(subject, Arg.Any<CancellationToken>())
            .Returns([new KrackendRoleAssignment { Id = "role-1", Provider = subject.Provider, SubjectId = subject.SubjectId, Role = KrackendRoles.Designer }]);
        permissionRepository.GetForSubject(subject, Arg.Any<CancellationToken>())
            .Returns([new KrackendPermissionAssignment { Id = "permission-1", Provider = subject.Provider, SubjectId = subject.SubjectId, Permission = KrackendPermissions.ControlPlaneRead }]);
        roleRepository.Upsert(Arg.Do<KrackendRoleAssignment>(x => capturedRole = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        permissionRepository.Upsert(Arg.Do<KrackendPermissionAssignment>(x => capturedPermission = x), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(subjectRepository);
                services.AddSingleton(roleRepository);
                services.AddSingleton(permissionRepository);
            },
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.RoutePrefix = "security";
                options.AuthorizationPolicy = string.Empty;
            }));

        var roles = await Invoke(
            app,
            "GET",
            "/security/subjects/{subjectId}/roles",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var missingRoles = await Invoke(
            app,
            "GET",
            "/security/subjects/{subjectId}/roles",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "missing" });
        var createdRole = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/roles",
            JsonSerializer.Serialize(new CreateKrackendRoleAssignmentRequest
            {
                Role = " designer ",
                ScopeType = string.Empty,
                ScopeId = " global ",
                Source = string.Empty
            }),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var missingRoleSubject = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/roles",
            JsonSerializer.Serialize(new CreateKrackendRoleAssignmentRequest { Role = KrackendRoles.Reader }),
            new Dictionary<string, object?> { ["subjectId"] = "missing" });
        var deletedRole = await Invoke(
            app,
            "DELETE",
            "/security/subjects/{subjectId}/roles/{assignmentId}",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "subject-1", ["assignmentId"] = "role-1" });
        var permissions = await Invoke(
            app,
            "GET",
            "/security/subjects/{subjectId}/permissions",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var createdPermission = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/permissions",
            JsonSerializer.Serialize(new CreateKrackendPermissionAssignmentRequest
            {
                Permission = " control-plane:read ",
                ScopeType = string.Empty,
                ScopeId = " all ",
                Source = string.Empty
            }),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var missingPermissionSubject = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/permissions",
            JsonSerializer.Serialize(new CreateKrackendPermissionAssignmentRequest { Permission = KrackendPermissions.ControlPlaneRead }),
            new Dictionary<string, object?> { ["subjectId"] = "missing" });
        var deletedPermission = await Invoke(
            app,
            "DELETE",
            "/security/subjects/{subjectId}/permissions/{assignmentId}",
            routeValues: new Dictionary<string, object?> { ["subjectId"] = "subject-1", ["assignmentId"] = "permission-1" });

        Assert.Equal(StatusCodes.Status200OK, roles.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missingRoles.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdRole.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missingRoleSubject.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, deletedRole.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, permissions.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdPermission.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missingPermissionSubject.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, deletedPermission.StatusCode);
        Assert.Contains(KrackendRoles.Designer, roles.Body, StringComparison.Ordinal);
        Assert.Contains(KrackendPermissions.ControlPlaneRead, permissions.Body, StringComparison.Ordinal);
        Assert.NotNull(capturedRole);
        Assert.Equal(subject.Provider, capturedRole.Provider);
        Assert.Equal(subject.SubjectId, capturedRole.SubjectId);
        Assert.Equal("designer", capturedRole.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, capturedRole.ScopeType);
        Assert.Equal("global", capturedRole.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, capturedRole.Source);
        Assert.NotNull(capturedPermission);
        Assert.Equal("control-plane:read", capturedPermission.Permission);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, capturedPermission.ScopeType);
        Assert.Equal("all", capturedPermission.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, capturedPermission.Source);

        await roleRepository.Received(1).Remove("role-1", Arg.Any<CancellationToken>());
        await permissionRepository.Received(1).Remove("permission-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecurityApiMapsCustomRoleAndPermissionAssignmentScopeAndSource()
    {
        var subjectRepository = Substitute.For<IKrackendSubjectRepository>();
        var roleRepository = Substitute.For<IKrackendRoleAssignmentRepository>();
        var permissionRepository = Substitute.For<IKrackendPermissionAssignmentRepository>();
        var subject = CreateSubject("subject-1");
        var capturedRoles = new List<KrackendRoleAssignment>();
        var capturedPermissions = new List<KrackendPermissionAssignment>();

        subjectRepository.GetById("subject-1", Arg.Any<CancellationToken>()).Returns(subject);
        roleRepository.Upsert(Arg.Do<KrackendRoleAssignment>(capturedRoles.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        permissionRepository.Upsert(Arg.Do<KrackendPermissionAssignment>(capturedPermissions.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var app = BuildApp(
            services =>
            {
                services.AddSingleton(subjectRepository);
                services.AddSingleton(roleRepository);
                services.AddSingleton(permissionRepository);
            },
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.RoutePrefix = "security";
                options.AuthorizationPolicy = string.Empty;
            }));

        var createdRole = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/roles",
            JsonSerializer.Serialize(new CreateKrackendRoleAssignmentRequest
            {
                Role = " reader ",
                ScopeType = " orchestration ",
                ScopeId = " orch-1 ",
                Source = " imported "
            }),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var createdPermission = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/permissions",
            JsonSerializer.Serialize(new CreateKrackendPermissionAssignmentRequest
            {
                Permission = " runtime:operate ",
                ScopeType = " runtime-node ",
                ScopeId = " runtime-1 ",
                Source = " synced "
            }),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var createdBlankRole = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/roles",
            JsonSerializer.Serialize(new CreateKrackendRoleAssignmentRequest()),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });
        var createdBlankPermission = await Invoke(
            app,
            "POST",
            "/security/subjects/{subjectId}/permissions",
            JsonSerializer.Serialize(new CreateKrackendPermissionAssignmentRequest()),
            new Dictionary<string, object?> { ["subjectId"] = "subject-1" });

        Assert.Equal(StatusCodes.Status201Created, createdRole.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdPermission.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdBlankRole.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdBlankPermission.StatusCode);
        var capturedRole = capturedRoles[0];
        Assert.Equal("reader", capturedRole.Role);
        Assert.Equal("orchestration", capturedRole.ScopeType);
        Assert.Equal("orch-1", capturedRole.ScopeId);
        Assert.Equal("imported", capturedRole.Source);
        var blankRole = capturedRoles[1];
        Assert.Equal(string.Empty, blankRole.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, blankRole.ScopeType);
        Assert.Equal(string.Empty, blankRole.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, blankRole.Source);
        var capturedPermission = capturedPermissions[0];
        Assert.Equal("runtime:operate", capturedPermission.Permission);
        Assert.Equal("runtime-node", capturedPermission.ScopeType);
        Assert.Equal("runtime-1", capturedPermission.ScopeId);
        Assert.Equal("synced", capturedPermission.Source);
        var blankPermission = capturedPermissions[1];
        Assert.Equal(string.Empty, blankPermission.Permission);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, blankPermission.ScopeType);
        Assert.Equal(string.Empty, blankPermission.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, blankPermission.Source);
    }

    [Fact]
    public async Task SecurityApiMapsExternalGroupRoleAssignments()
    {
        var repository = Substitute.For<IKrackendExternalGroupRoleAssignmentRepository>();
        var capturedAssignments = new List<KrackendExternalGroupRoleAssignment>();
        repository.GetAll(Arg.Any<CancellationToken>())
            .Returns([new KrackendExternalGroupRoleAssignment { Id = "external-1", Provider = "entra", ExternalGroupId = "group-1", Role = KrackendRoles.Reader }]);
        repository.Upsert(Arg.Do<KrackendExternalGroupRoleAssignment>(capturedAssignments.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var app = BuildApp(
            services => services.AddSingleton(repository),
            endpoints => endpoints.MapKrackendSecurityAdministrationApi(options =>
            {
                options.RoutePrefix = "/security";
                options.AuthorizationPolicy = string.Empty;
            }));

        var all = await Invoke(app, "GET", "/security/external-groups");
        var created = await Invoke(
            app,
            "POST",
            "/security/external-groups/{provider}/{groupId}/roles",
            JsonSerializer.Serialize(new CreateKrackendExternalGroupRoleAssignmentRequest
            {
                Role = " reader ",
                ScopeType = string.Empty,
                ScopeId = " tenant ",
                Source = " sync "
            }),
            new Dictionary<string, object?> { ["provider"] = " entra ", ["groupId"] = " group-2 " });
        var createdBlank = await Invoke(
            app,
            "POST",
            "/security/external-groups/{provider}/{groupId}/roles",
            JsonSerializer.Serialize(new CreateKrackendExternalGroupRoleAssignmentRequest()),
            new Dictionary<string, object?> { ["provider"] = null, ["groupId"] = null });
        var deleted = await Invoke(
            app,
            "DELETE",
            "/security/external-groups/{assignmentId}",
            routeValues: new Dictionary<string, object?> { ["assignmentId"] = "external-1" });

        Assert.Equal(StatusCodes.Status200OK, all.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, createdBlank.StatusCode);
        Assert.Equal(StatusCodes.Status204NoContent, deleted.StatusCode);
        Assert.Contains("group-1", all.Body, StringComparison.Ordinal);
        var capturedAssignment = capturedAssignments[0];
        Assert.Equal("entra", capturedAssignment.Provider);
        Assert.Equal("group-2", capturedAssignment.ExternalGroupId);
        Assert.Equal("reader", capturedAssignment.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, capturedAssignment.ScopeType);
        Assert.Equal("tenant", capturedAssignment.ScopeId);
        Assert.Equal("sync", capturedAssignment.Source);
        var blankAssignment = capturedAssignments[1];
        Assert.Equal(string.Empty, blankAssignment.Provider);
        Assert.Equal(string.Empty, blankAssignment.ExternalGroupId);
        Assert.Equal(string.Empty, blankAssignment.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, blankAssignment.ScopeType);
        Assert.Equal(string.Empty, blankAssignment.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, blankAssignment.Source);
        await repository.Received(1).Remove("external-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SecurityApiPrivateMappersNormalizeNullAssignmentFields()
    {
        var subject = CreateSubject("subject-1");
        var role = InvokePrivate<KrackendRoleAssignment>(
            "ToRoleAssignment",
            [typeof(KrackendSubject), typeof(CreateKrackendRoleAssignmentRequest)],
            [
                subject,
                new CreateKrackendRoleAssignmentRequest
                {
                    Role = null!,
                    ScopeType = null!,
                    ScopeId = null!,
                    Source = null!
                }
            ]);
        var permission = InvokePrivate<KrackendPermissionAssignment>(
            "ToPermissionAssignment",
            [typeof(KrackendSubject), typeof(CreateKrackendPermissionAssignmentRequest)],
            [
                subject,
                new CreateKrackendPermissionAssignmentRequest
                {
                    Permission = null!,
                    ScopeType = null!,
                    ScopeId = null!,
                    Source = null!
                }
            ]);
        var external = InvokePrivate<KrackendExternalGroupRoleAssignment>(
            "ToExternalGroupRoleAssignment",
            [typeof(string), typeof(string), typeof(CreateKrackendExternalGroupRoleAssignmentRequest)],
            [
                null,
                null,
                new CreateKrackendExternalGroupRoleAssignmentRequest
                {
                    Role = null!,
                    ScopeType = null!,
                    ScopeId = null!,
                    Source = null!
                }
            ]);

        Assert.Equal(string.Empty, role.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, role.ScopeType);
        Assert.Equal(string.Empty, role.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, role.Source);
        Assert.Equal(string.Empty, permission.Permission);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, permission.ScopeType);
        Assert.Equal(string.Empty, permission.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, permission.Source);
        Assert.Equal(string.Empty, external.Provider);
        Assert.Equal(string.Empty, external.ExternalGroupId);
        Assert.Equal(string.Empty, external.Role);
        Assert.Equal(KrackendAuthorizationScopeTypes.Global, external.ScopeType);
        Assert.Equal(string.Empty, external.ScopeId);
        Assert.Equal(KrackendAssignmentSources.Manual, external.Source);
    }

    private static KrackendSubject CreateSubject(string id)
        => new()
        {
            Id = id,
            Provider = "entra",
            SubjectId = id,
            DisplayName = "Alice Example",
            Email = "alice@example.test",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static WebApplication BuildApp(Action<IServiceCollection> configureServices, Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        configureServices(builder.Services);
        var app = builder.Build();
        map(app);
        return app;
    }

    private static async Task<EndpointInvocationResult> Invoke(
        WebApplication app,
        string method,
        string routePattern,
        string body = "",
        Dictionary<string, object?>? routeValues = null,
        string queryString = "")
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x =>
                string.Equals(x.RoutePattern.RawText, routePattern, StringComparison.Ordinal) &&
                (x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) ?? true));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = app.Services
        };
        httpContext.SetEndpoint(endpoint);
        httpContext.Request.Method = method;
        httpContext.Request.Path = routePattern.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal);
        httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Response.Body = new MemoryStream();

        foreach (var pair in routeValues ?? [])
        {
            httpContext.Request.RouteValues[pair.Key] = pair.Value;
        }

        if (body.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Features.Set<IHttpRequestBodyDetectionFeature>(
                new TestHttpRequestBodyDetectionFeature(canHaveBody: true));
        }

        await endpoint.RequestDelegate!(httpContext);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
        return new EndpointInvocationResult(httpContext.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static TResult InvokePrivate<TResult>(
        string methodName,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var method = typeof(Krackend.Sagas.Orchestrations.Security.Api.EndpointRouteBuilderExtensions)
            .GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
                modifiers: null)!;
        return (TResult)method.Invoke(null, arguments)!;
    }

    private sealed record EndpointInvocationResult(int StatusCode, string Body);

    private sealed class TestHttpRequestBodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
