using Shared.Primitives.Identifiers;

namespace Core.Authorization;

public enum PermissionScopeType { Tenant, LegalEntity, Branch }
public readonly record struct PermissionScope(PermissionScopeType Type, TenantId TenantId, LegalEntityId? LegalEntityId = null, BranchId? BranchId = null)
{
    public static PermissionScope ForTenant(TenantId tenantId) => new(PermissionScopeType.Tenant, tenantId);
    public static PermissionScope ForLegalEntity(TenantId tenantId, LegalEntityId legalEntityId) => new(PermissionScopeType.LegalEntity, tenantId, legalEntityId);
    public static PermissionScope ForBranch(TenantId tenantId, LegalEntityId legalEntityId, BranchId branchId) => new(PermissionScopeType.Branch, tenantId, legalEntityId, branchId);
    public bool Contains(PermissionScope resource) => Type switch
    {
        PermissionScopeType.Tenant => TenantId == resource.TenantId,
        PermissionScopeType.LegalEntity => TenantId == resource.TenantId && LegalEntityId == resource.LegalEntityId,
        PermissionScopeType.Branch => TenantId == resource.TenantId && LegalEntityId == resource.LegalEntityId && BranchId == resource.BranchId,
        _ => false
    };
}

public sealed record Role(Guid Id, TenantId TenantId, string Code, string Name, bool IsSystemTemplate, bool IsActive, long Version);
public sealed record RolePermissionEntry(TenantId TenantId, Guid RoleId, string PermissionCode);
public sealed record MembershipRoleAssignment(TenantId TenantId, Guid MembershipId, Guid RoleId, PermissionScope Scope);
public sealed record AuthorizationSubject(TenantId TenantId, Guid MembershipId, IReadOnlyCollection<MembershipRoleAssignment> Assignments, IReadOnlyCollection<Role> Roles, IReadOnlyCollection<RolePermissionEntry> Permissions, int Version);

public interface IPermissionAuthorizer
{
    bool Authorize(AuthorizationSubject subject, string permissionCode, PermissionScope resourceScope);
    void Invalidate(TenantId tenantId, Guid membershipId);
}

public sealed class PermissionAuthorizer : IPermissionAuthorizer
{
    public bool Authorize(AuthorizationSubject subject, string permissionCode, PermissionScope resourceScope)
    {
        if (subject.TenantId != resourceScope.TenantId || string.IsNullOrWhiteSpace(permissionCode) || permissionCode is "*" or "admin") return false;
        return subject.Roles.Any(role =>
            role.TenantId == subject.TenantId
            && role.IsActive
            && subject.Assignments.Any(assignment =>
                assignment.TenantId == subject.TenantId
                && assignment.MembershipId == subject.MembershipId
                && assignment.RoleId == role.Id
                && assignment.Scope.Contains(resourceScope))
            && subject.Permissions.Any(permission =>
                permission.TenantId == subject.TenantId
                && permission.RoleId == role.Id
                && permission.PermissionCode == permissionCode));
    }

    // Authorization subjects are loaded from the authoritative store for each request. Keeping no process-wide decision cache prevents role, assignment, and lifecycle changes from remaining effective after a revocation.
    public void Invalidate(TenantId tenantId, Guid membershipId) { }
}
