using Todir.SharedKernel.Identifiers;

namespace Todir.BuildingBlocks.Organization;

public enum OrganizationScopeKind
{
    Tenant,
    LegalEntity,
    Branch
}

/// <summary>Represents the minimum organization scope context used by platform boundaries.</summary>
public readonly record struct OrganizationScope(
    OrganizationScopeKind Kind,
    TenantId TenantId,
    LegalEntityId? LegalEntityId = null,
    BranchId? BranchId = null)
{
    public static OrganizationScope ForTenant(TenantId tenantId) => new(OrganizationScopeKind.Tenant, tenantId);

    public static OrganizationScope ForLegalEntity(TenantId tenantId, LegalEntityId legalEntityId) =>
        new(OrganizationScopeKind.LegalEntity, tenantId, legalEntityId);

    public static OrganizationScope ForBranch(TenantId tenantId, LegalEntityId legalEntityId, BranchId branchId) =>
        new(OrganizationScopeKind.Branch, tenantId, legalEntityId, branchId);

    /// <summary>Checks whether a resource is inside this context without widening a narrower scope.</summary>
    public bool Includes(TenantId tenantId, LegalEntityId? legalEntityId = null, BranchId? branchId = null) =>
        Kind switch
        {
            OrganizationScopeKind.Tenant => TenantId == tenantId,
            OrganizationScopeKind.LegalEntity => TenantId == tenantId && LegalEntityId == legalEntityId,
            OrganizationScopeKind.Branch => TenantId == tenantId && LegalEntityId == legalEntityId && BranchId == branchId,
            _ => false
        };
}
