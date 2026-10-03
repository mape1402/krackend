using Krackend.Sagas.Orchestrations.Security.Core;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Security;

public sealed class EntityFrameworkKrackendAssignmentRepositoryTests
{
    [Fact]
    public async Task PermissionAssignmentRepositoryNormalizesUpdatesQueriesAndRemovesAssignments()
    {
        await using var provider = CreateProvider("permission");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendPermissionAssignmentRepository>();
        var assignment = new KrackendPermissionAssignment
        {
            Provider = " oidc ",
            SubjectId = " user-1 ",
            Permission = " control-plane.read ",
            ScopeType = " control-plane ",
            ScopeId = " root ",
            Source = " manual ",
            IsEnabled = true
        };

        await repository.Upsert(assignment);

        var enabled = await repository.GetForSubject("oidc", "user-1");
        Assert.False(string.IsNullOrWhiteSpace(assignment.Id));
        Assert.Equal("manual", Assert.Single(enabled).Source);

        await repository.Upsert(new KrackendPermissionAssignment
        {
            Provider = "oidc",
            SubjectId = "user-1",
            Permission = "control-plane.read",
            ScopeType = "control-plane",
            ScopeId = "root",
            Source = "bootstrap",
            IsEnabled = false
        });

        Assert.Empty(await repository.GetForSubject(new KrackendSubject
        {
            Provider = " oidc ",
            SubjectId = " user-1 "
        }));
        Assert.Empty(await repository.GetForSubject(null!));

        assignment.IsEnabled = true;
        assignment.Source = "restored";
        await repository.Upsert(assignment);
        await repository.Remove("missing");
        await repository.Remove(assignment.Id);

        Assert.Empty(await repository.GetForSubject("oidc", "user-1"));
    }

    [Fact]
    public async Task PermissionAssignmentRepositoryNormalizesNullFieldsAndNullLookupArguments()
    {
        await using var provider = CreateProvider("permission-null");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendPermissionAssignmentRepository>();

        await repository.Upsert(new KrackendPermissionAssignment
        {
            Provider = null!,
            SubjectId = null!,
            Permission = null!,
            ScopeType = null!,
            ScopeId = null!,
            Source = null!,
            IsEnabled = true
        });

        var assignments = await repository.GetForSubject(null!, null!);

        var assignment = Assert.Single(assignments);
        Assert.Equal(string.Empty, assignment.Provider);
        Assert.Equal(string.Empty, assignment.SubjectId);
        Assert.Equal(string.Empty, assignment.Permission);
        Assert.Equal(string.Empty, assignment.Source);
    }

    [Fact]
    public async Task RoleAssignmentRepositoryNormalizesUpdatesQueriesAndRemovesAssignments()
    {
        await using var provider = CreateProvider("role");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendRoleAssignmentRepository>();
        var assignment = new KrackendRoleAssignment
        {
            Provider = " oidc ",
            SubjectId = " user-2 ",
            Role = " designer ",
            ScopeType = " control-plane ",
            ScopeId = " root ",
            Source = " manual ",
            IsEnabled = true
        };

        await repository.Upsert(assignment);

        var enabled = await repository.GetForSubject("oidc", "user-2");
        Assert.False(string.IsNullOrWhiteSpace(assignment.Id));
        Assert.Equal("designer", Assert.Single(enabled).Role);

        await repository.Upsert(new KrackendRoleAssignment
        {
            Provider = "oidc",
            SubjectId = "user-2",
            Role = "designer",
            ScopeType = "control-plane",
            ScopeId = "root",
            Source = "bootstrap",
            IsEnabled = false
        });

        Assert.Empty(await repository.GetForSubject(new KrackendSubject
        {
            Provider = " oidc ",
            SubjectId = " user-2 "
        }));
        Assert.Empty(await repository.GetForSubject(null!));

        assignment.IsEnabled = true;
        assignment.Source = "restored";
        await repository.Upsert(assignment);
        await repository.Remove("missing");
        await repository.Remove(assignment.Id);

        Assert.Empty(await repository.GetForSubject("oidc", "user-2"));
    }

    [Fact]
    public async Task RoleAssignmentRepositoryNormalizesNullFieldsAndNullLookupArguments()
    {
        await using var provider = CreateProvider("role-null");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendRoleAssignmentRepository>();

        await repository.Upsert(new KrackendRoleAssignment
        {
            Provider = null!,
            SubjectId = null!,
            Role = null!,
            ScopeType = null!,
            ScopeId = null!,
            Source = null!,
            IsEnabled = true
        });

        var assignments = await repository.GetForSubject(null!, null!);

        var assignment = Assert.Single(assignments);
        Assert.Equal(string.Empty, assignment.Provider);
        Assert.Equal(string.Empty, assignment.SubjectId);
        Assert.Equal(string.Empty, assignment.Role);
        Assert.Equal(string.Empty, assignment.Source);
    }

    [Fact]
    public async Task ExternalGroupRoleAssignmentRepositoryNormalizesUpdatesQueriesAndRemovesAssignments()
    {
        await using var provider = CreateProvider("external-group");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendExternalGroupRoleAssignmentRepository>();
        var assignment = new KrackendExternalGroupRoleAssignment
        {
            Provider = " oidc ",
            ExternalGroupId = " group-a ",
            Role = " runtime-operator ",
            ScopeType = " runtime ",
            ScopeId = " main ",
            Source = " manual ",
            IsEnabled = true
        };

        await repository.Upsert(assignment);

        var byGroups = await repository.GetForGroups(" oidc ", [" group-a ", "GROUP-A", " "]);
        var all = await repository.GetAll();
        Assert.False(string.IsNullOrWhiteSpace(assignment.Id));
        Assert.Equal("group-a", Assert.Single(byGroups).ExternalGroupId);
        Assert.Equal("runtime-operator", Assert.Single(all).Role);
        Assert.Empty(await repository.GetForGroups("oidc", []));
        Assert.Empty(await repository.GetForGroups("oidc", null!));

        await repository.Upsert(new KrackendExternalGroupRoleAssignment
        {
            Provider = "oidc",
            ExternalGroupId = "group-a",
            Role = "runtime-operator",
            ScopeType = "runtime",
            ScopeId = "main",
            Source = "bootstrap",
            IsEnabled = false
        });

        Assert.Empty(await repository.GetForGroups("oidc", ["group-a"]));
        Assert.Empty(await repository.GetAll());

        assignment.IsEnabled = true;
        assignment.Source = "restored";
        await repository.Upsert(assignment);
        await repository.Remove("missing");
        await repository.Remove(assignment.Id);

        Assert.Empty(await repository.GetAll());
    }

    [Fact]
    public async Task ExternalGroupRoleAssignmentRepositoryNormalizesNullFieldsAndNullLookupArguments()
    {
        await using var provider = CreateProvider("external-group-null");
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IKrackendExternalGroupRoleAssignmentRepository>();

        await repository.Upsert(new KrackendExternalGroupRoleAssignment
        {
            Provider = null!,
            ExternalGroupId = null!,
            Role = null!,
            ScopeType = null!,
            ScopeId = null!,
            Source = null!,
            IsEnabled = true
        });

        var assignments = await repository.GetForGroups(null!, [null!, " "]);

        Assert.Empty(assignments);
        var stored = Assert.Single(await repository.GetAll());
        Assert.Equal(string.Empty, stored.Provider);
        Assert.Equal(string.Empty, stored.ExternalGroupId);
        Assert.Equal(string.Empty, stored.Role);
        Assert.Equal(string.Empty, stored.Source);
    }

    private static ServiceProvider CreateProvider(string name)
    {
        var services = new ServiceCollection();
        services.AddKrackendSecurityStorageEntityFramework(options =>
            options.UseInMemoryDatabase($"security-assignments-{name}-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }
}
