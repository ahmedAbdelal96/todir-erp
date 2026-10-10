using Shared.BuildingBlocks.Organization;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Shared.Primitives.Tests;

public sealed class OrganizationScopeTests
{
    [Fact]
    public void ScopeContextsDistinguishTenantEntityAndBranchAccess()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var entity = LegalEntityId.New();
        var otherEntity = LegalEntityId.New();
        var branch = BranchId.New();
        var otherBranch = BranchId.New();

        Assert.True(OrganizationScope.ForTenant(tenant).Includes(tenant, otherEntity, otherBranch));
        Assert.False(OrganizationScope.ForTenant(tenant).Includes(otherTenant, entity, branch));

        Assert.True(OrganizationScope.ForLegalEntity(tenant, entity).Includes(tenant, entity, otherBranch));
        Assert.False(OrganizationScope.ForLegalEntity(tenant, entity).Includes(tenant, otherEntity, branch));

        Assert.True(OrganizationScope.ForBranch(tenant, entity, branch).Includes(tenant, entity, branch));
        Assert.False(OrganizationScope.ForBranch(tenant, entity, branch).Includes(tenant, entity, otherBranch));
        Assert.False(OrganizationScope.ForBranch(tenant, entity, branch).Includes(tenant, null, branch));
    }
}
