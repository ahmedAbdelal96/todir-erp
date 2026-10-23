using System.Security.Claims;
using Core.Authorization;
using Shared.Primitives.Identifiers;

namespace Modules.Tax.Services;

public sealed record TaxAccess(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);

/// <summary>Resolves a tenant-bound Tax permission for one Legal Entity.</summary>
public static class TaxAuthorization
{
    public static async Task<TaxAccess?> ResolveAsync(ClaimsPrincipal user, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        string permission, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(user.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, cancellationToken);
        return subject is null ? null : new(tenant, actorId, membershipId,
            authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(legalEntityId))));
    }

    public static async Task<TaxAccess?> ResolveTenantAsync(ClaimsPrincipal user, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        string permission, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(user.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, cancellationToken);
        return subject is null ? null : new(tenant, actorId, membershipId,
            authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant)));
    }
}
