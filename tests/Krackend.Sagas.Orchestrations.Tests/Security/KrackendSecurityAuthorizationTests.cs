using System.Security.Claims;
using Krackend.Security.Authorization;
using Krackend.Security.Configuration;
using Krackend.Security.Core;
using Krackend.Security.DependencyInjection;
using Krackend.Security.Storage;
using Krackend.Security.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class KrackendSecurityAuthorizationTests
{
    [Fact]
    public async Task BootstrapAdminReceivesAdminPermissionAndIsSynced()
    {
        await using var provider = BuildProvider(options =>
        {
            options.RequireKnownSubject = true;
            options.BootstrapAdmins.Add(new KrackendBootstrapSubject
            {
                Provider = "test",
                SubjectId = "admin-1"
            });
        });

        using var scope = provider.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IKrackendAuthorizationService>();
        var subjectRepository = scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IKrackendRoleAssignmentRepository>();

        var allowed = await authorization.HasPermission(
            Principal("admin-1"),
            KrackendPermissions.ControlPlaneSecurityManage,
            KrackendAuthorizationScope.ControlPlane());

        var subject = await subjectRepository.GetByExternalSubject("test", "admin-1");
        var roles = await roleRepository.GetForSubject("test", "admin-1");

        Assert.True(allowed);
        Assert.NotNull(subject);
        Assert.Contains(roles, x => x.Role == KrackendRoles.Admin && x.Source == KrackendAssignmentSources.BootstrapConfig);
    }

    [Fact]
    public async Task UnknownSubjectIsDeniedWhenProductPermissionIsRequired()
    {
        await using var provider = BuildProvider(options => options.RequireKnownSubject = false);

        using var scope = provider.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IKrackendAuthorizationService>();

        var allowed = await authorization.HasPermission(
            Principal("unknown"),
            KrackendPermissions.ControlPlaneRead,
            KrackendAuthorizationScope.ControlPlane());

        Assert.False(allowed);
    }

    [Fact]
    public async Task DisabledSubjectIsDeniedEvenWithAssignments()
    {
        await using var provider = BuildProvider(_ => { });
        using var scope = provider.CreateScope();

        var subject = new KrackendSubject
        {
            Id = "subject-1",
            Provider = "test",
            SubjectId = "disabled-1",
            DisplayName = "Disabled",
            IsEnabled = false
        };

        await scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>().Upsert(subject);
        await scope.ServiceProvider.GetRequiredService<IKrackendPermissionAssignmentRepository>().Upsert(new KrackendPermissionAssignment
        {
            Id = "permission-1",
            Provider = "test",
            SubjectId = "disabled-1",
            Permission = KrackendPermissions.ControlPlaneRead,
            ScopeType = KrackendAuthorizationScopeTypes.ControlPlane,
            IsEnabled = true
        });

        var allowed = await scope.ServiceProvider.GetRequiredService<IKrackendAuthorizationService>().HasPermission(
            Principal("disabled-1"),
            KrackendPermissions.ControlPlaneRead,
            KrackendAuthorizationScope.ControlPlane());

        Assert.False(allowed);
    }

    [Fact]
    public async Task DirectPermissionGrantsMatchingScopeOnly()
    {
        await using var provider = BuildProvider(_ => { });
        using var scope = provider.CreateScope();

        await AddSubject(scope, "subject-1", "subject-1");
        await scope.ServiceProvider.GetRequiredService<IKrackendPermissionAssignmentRepository>().Upsert(new KrackendPermissionAssignment
        {
            Id = "permission-1",
            Provider = "test",
            SubjectId = "subject-1",
            Permission = KrackendPermissions.ControlPlaneRead,
            ScopeType = KrackendAuthorizationScopeTypes.ControlPlane,
            IsEnabled = true
        });

        var authorization = scope.ServiceProvider.GetRequiredService<IKrackendAuthorizationService>();

        Assert.True(await authorization.HasPermission(Principal("subject-1"), KrackendPermissions.ControlPlaneRead, KrackendAuthorizationScope.ControlPlane()));
        Assert.False(await authorization.HasPermission(Principal("subject-1"), KrackendPermissions.RuntimeRead, KrackendAuthorizationScope.Runtime()));
    }

    [Fact]
    public async Task DirectRoleExternalGroupRoleAndAdminWildcardGrantPermissions()
    {
        await using var provider = BuildProvider(_ => { });
        using var scope = provider.CreateScope();

        await AddSubject(scope, "designer", "designer");
        await AddSubject(scope, "group-user", "group-user");
        await AddSubject(scope, "admin", "admin");

        var roleRepository = scope.ServiceProvider.GetRequiredService<IKrackendRoleAssignmentRepository>();
        await roleRepository.Upsert(new KrackendRoleAssignment
        {
            Id = "role-1",
            Provider = "test",
            SubjectId = "designer",
            Role = KrackendRoles.Designer,
            ScopeType = KrackendAuthorizationScopeTypes.ControlPlane,
            IsEnabled = true
        });
        await roleRepository.Upsert(new KrackendRoleAssignment
        {
            Id = "role-2",
            Provider = "test",
            SubjectId = "admin",
            Role = KrackendRoles.Admin,
            ScopeType = KrackendAuthorizationScopeTypes.Global,
            IsEnabled = true
        });
        await scope.ServiceProvider.GetRequiredService<IKrackendExternalGroupRoleAssignmentRepository>().Upsert(new KrackendExternalGroupRoleAssignment
        {
            Id = "group-role-1",
            Provider = "test",
            ExternalGroupId = "ops",
            Role = KrackendRoles.RuntimeOperator,
            ScopeType = KrackendAuthorizationScopeTypes.Runtime,
            IsEnabled = true
        });

        var authorization = scope.ServiceProvider.GetRequiredService<IKrackendAuthorizationService>();

        Assert.True(await authorization.HasPermission(Principal("designer"), KrackendPermissions.ControlPlaneDesignWrite, KrackendAuthorizationScope.ControlPlane()));
        Assert.True(await authorization.HasPermission(Principal("group-user", "ops"), KrackendPermissions.RuntimeManage, KrackendAuthorizationScope.Runtime()));
        Assert.True(await authorization.HasPermission(Principal("admin"), KrackendPermissions.RuntimeArtifactsApply, KrackendAuthorizationScope.Runtime()));
    }

    private static async Task AddSubject(IServiceScope scope, string id, string externalSubjectId)
        => await scope.ServiceProvider.GetRequiredService<IKrackendSubjectRepository>().Upsert(new KrackendSubject
        {
            Id = id,
            Provider = "test",
            SubjectId = externalSubjectId,
            DisplayName = externalSubjectId,
            IsEnabled = true
        });

    private static ServiceProvider BuildProvider(Action<KrackendSecurityOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddKrackendSecurity(options =>
        {
            options.Subject.Provider = "test";
            configure(options);
        });
        services.AddKrackendSecurityStorageEntityFramework(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(string subjectId, params string[] groups)
    {
        var claims = new List<Claim>
        {
            new("oid", subjectId),
            new(ClaimTypes.Name, subjectId)
        };
        claims.AddRange(groups.Select(group => new Claim("groups", group)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
