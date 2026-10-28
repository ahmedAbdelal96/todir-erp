using Core.Authorization;
using Modules.Inventory.Entities;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Modules.Inventory.Tests;

public sealed class InventoryClosureTests
{
    [Fact]
    public void ApprovalModesHaveExplicitPostingStates()
    {
        Assert.Equal("DirectConfirm", AdjustmentApprovalMode.DirectConfirm.ToString());
        Assert.Equal("ApprovalRequired", AdjustmentApprovalMode.ApprovalRequired.ToString());
        Assert.Equal("PendingApproval", AdjustmentRequestStatus.PendingApproval.ToString());
        Assert.Equal("Posted", AdjustmentRequestStatus.Posted.ToString());
    }

    [Fact]
    public void BranchScopedInventoryPermissionCannotCrossWarehouseBranch()
    {
        var tenant = TenantId.New();
        var entity = new LegalEntityId(Guid.CreateVersion7());
        var permittedBranch = new BranchId(Guid.CreateVersion7());
        var otherBranch = new BranchId(Guid.CreateVersion7());
        var roleId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        var subject = new AuthorizationSubject(tenant, membershipId,
            [new MembershipRoleAssignment(tenant, membershipId, roleId, PermissionScope.ForBranch(tenant, entity, permittedBranch))],
            [new Role(roleId, tenant, "warehouse-operator", "Warehouse operator", false, true, 1)],
            [new RolePermissionEntry(tenant, roleId, "inventory.receipt.confirm")], 1);
        var authorizer = new PermissionAuthorizer();

        Assert.True(authorizer.Authorize(subject, "inventory.receipt.confirm", PermissionScope.ForBranch(tenant, entity, permittedBranch)));
        Assert.False(authorizer.Authorize(subject, "inventory.receipt.confirm", PermissionScope.ForBranch(tenant, entity, otherBranch)));
        Assert.False(authorizer.Authorize(subject, "inventory.receipt.confirm", PermissionScope.ForLegalEntity(tenant, entity)));
    }
}
