using System.Security.Claims;
using Krackend.Security.Configuration;
using Krackend.Security.Core;
using Krackend.Security.Storage;
using Krackend.Security.Subjects;
using Microsoft.Extensions.Options;

namespace Krackend.Security.Authorization;

/// <summary>
/// Default provider-agnostic product authorization service.
/// </summary>
public sealed class KrackendAuthorizationService : IKrackendAuthorizationService
{
    private readonly IKrackendSubjectResolver _subjectResolver;
    private readonly IKrackendSubjectRepository _subjectRepository;
    private readonly IKrackendRoleAssignmentRepository _roleAssignmentRepository;
    private readonly IKrackendPermissionAssignmentRepository _permissionAssignmentRepository;
    private readonly IKrackendExternalGroupRoleAssignmentRepository _externalGroupRoleAssignmentRepository;
    private readonly IKrackendRolePermissionCatalog _rolePermissionCatalog;
    private readonly IOptions<KrackendSecurityOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendAuthorizationService"/> class.
    /// </summary>
    public KrackendAuthorizationService(
        IKrackendSubjectResolver subjectResolver,
        IKrackendSubjectRepository subjectRepository,
        IKrackendRoleAssignmentRepository roleAssignmentRepository,
        IKrackendPermissionAssignmentRepository permissionAssignmentRepository,
        IKrackendExternalGroupRoleAssignmentRepository externalGroupRoleAssignmentRepository,
        IKrackendRolePermissionCatalog rolePermissionCatalog,
        IOptions<KrackendSecurityOptions> options)
    {
        _subjectResolver = subjectResolver ?? throw new ArgumentNullException(nameof(subjectResolver));
        _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
        _roleAssignmentRepository = roleAssignmentRepository ?? throw new ArgumentNullException(nameof(roleAssignmentRepository));
        _permissionAssignmentRepository = permissionAssignmentRepository ?? throw new ArgumentNullException(nameof(permissionAssignmentRepository));
        _externalGroupRoleAssignmentRepository = externalGroupRoleAssignmentRepository ?? throw new ArgumentNullException(nameof(externalGroupRoleAssignmentRepository));
        _rolePermissionCatalog = rolePermissionCatalog ?? throw new ArgumentNullException(nameof(rolePermissionCatalog));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<bool> HasPermission(
        ClaimsPrincipal user,
        string permission,
        KrackendAuthorizationScope scope,
        CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var externalSubject = _subjectResolver.Resolve(user);
        if (!externalSubject.IsResolved)
        {
            return false;
        }

        var isBootstrapAdmin = IsBootstrapAdmin(externalSubject);
        if (isBootstrapAdmin)
        {
            await SyncBootstrapAdmin(externalSubject, cancellationToken);
            return true;
        }

        var subject = await _subjectRepository.GetByExternalSubject(externalSubject.Provider, externalSubject.SubjectId, cancellationToken);
        if (subject is null || !subject.IsEnabled)
        {
            return false;
        }

        var permissionAssignments = await _permissionAssignmentRepository.GetForSubject(subject, cancellationToken);
        if (permissionAssignments.Any(x => MatchesPermission(x.Permission, permission) && MatchesScope(x.ScopeType, x.ScopeId, scope)))
        {
            return true;
        }

        var directRoles = await _roleAssignmentRepository.GetForSubject(subject, cancellationToken);
        if (directRoles.Any(x => RoleGrantsPermission(x.Role, permission) && MatchesScope(x.ScopeType, x.ScopeId, scope)))
        {
            return true;
        }

        var groupRoles = await _externalGroupRoleAssignmentRepository.GetForGroups(externalSubject.Provider, externalSubject.GroupIds, cancellationToken);
        return groupRoles.Any(x => RoleGrantsPermission(x.Role, permission) && MatchesScope(x.ScopeType, x.ScopeId, scope));
    }

    private bool IsBootstrapAdmin(KrackendExternalSubject subject)
        => _options.Value.BootstrapAdmins.Any(x =>
            string.Equals(x.Provider, subject.Provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SubjectId, subject.SubjectId, StringComparison.OrdinalIgnoreCase));

    private async Task SyncBootstrapAdmin(KrackendExternalSubject externalSubject, CancellationToken cancellationToken)
    {
        if (!_options.Value.AllowBootstrapAdminSync)
        {
            return;
        }

        var subject = await _subjectRepository.GetByExternalSubject(externalSubject.Provider, externalSubject.SubjectId, cancellationToken);
        subject ??= new KrackendSubject
        {
            Id = NewId(),
            Provider = externalSubject.Provider,
            SubjectId = externalSubject.SubjectId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        subject.DisplayName = externalSubject.DisplayName;
        subject.Email = externalSubject.Email;
        subject.IsEnabled = true;
        subject.UpdatedAtUtc = DateTime.UtcNow;
        await _subjectRepository.Upsert(subject, cancellationToken);

        var roles = await _roleAssignmentRepository.GetForSubject(subject, cancellationToken);
        if (roles.Any(x =>
            string.Equals(x.Role, KrackendRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ScopeType, KrackendAuthorizationScopeTypes.Global, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await _roleAssignmentRepository.Upsert(new KrackendRoleAssignment
        {
            Id = NewId(),
            Provider = externalSubject.Provider,
            SubjectId = externalSubject.SubjectId,
            Role = KrackendRoles.Admin,
            ScopeType = KrackendAuthorizationScopeTypes.Global,
            ScopeId = string.Empty,
            Source = KrackendAssignmentSources.BootstrapConfig,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
        }, cancellationToken);
    }

    private bool RoleGrantsPermission(string role, string permission)
    {
        var permissions = _rolePermissionCatalog.GetPermissions(role);
        return permissions.Any(x => MatchesPermission(x, permission));
    }

    private static bool MatchesPermission(string grantedPermission, string requiredPermission)
        => string.Equals(grantedPermission, KrackendPermissions.Wildcard, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(grantedPermission, requiredPermission, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesScope(string assignmentScopeType, string assignmentScopeId, KrackendAuthorizationScope requiredScope)
    {
        if (string.Equals(assignmentScopeType, KrackendAuthorizationScopeTypes.Global, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(assignmentScopeType, requiredScope.ScopeType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(assignmentScopeId) ||
               string.Equals(assignmentScopeId, requiredScope.ScopeId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string NewId()
        => Guid.NewGuid().ToString("N");
}
