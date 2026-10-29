using Modules.Purchasing.DTOs;
using Modules.Purchasing.Entities;
using Xunit;

namespace PurchasingTests;

public sealed class PurchasingDomainTests
{
    [Fact]
    public void PurchaseOrderLifecycleKeepsCommercialAndApprovalStatesSeparate()
    {
        Assert.Equal("Draft", PurchaseOrderStates.Draft);
        Assert.Equal("PendingApproval", PurchaseOrderStates.PendingApproval);
        Assert.Equal("Approved", PurchaseOrderStates.Approved);
        Assert.Equal("Closed", PurchaseOrderStates.Closed);
    }

    [Fact]
    public void PurchaseLineUsesDecimalQuantitiesAndPrices()
    {
        var line = new PurchaseOrderLineRequest(Guid.NewGuid(), Guid.NewGuid(), "EA", "مواد تشغيل", 2.5m, 100.25m, null);
        Assert.Equal(250.625m, line.Quantity * line.UnitPrice);
        Assert.True(line.Quantity > 0m);
    }

    [Fact]
    public void CommandCarriesOptimisticVersionAndReplayKey()
    {
        var command = new PurchaseOrderCommandRequest(4, "approve-po-4", "مراجعة المورد");
        Assert.Equal(4, command.Version);
        Assert.Equal("approve-po-4", command.IdempotencyKey);
    }
}
