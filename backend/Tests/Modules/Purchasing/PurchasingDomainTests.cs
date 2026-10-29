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

    [Fact]
    public void PartialSupplierInvoiceCarriesLineIdentityAndQuantity()
    {
        var lineId = Guid.NewGuid();
        var request = new CreateSupplierInvoiceRequest(7, "invoice-7", new(2026, 10, 29), new(2026, 10, 29), new(2026, 10, 29), "SUP-7", [new(lineId, 2.25m)]);

        Assert.Single(request.Lines!);
        Assert.Equal(lineId, request.Lines![0].PurchaseOrderLineId);
        Assert.Equal(2.25m, request.Lines[0].Quantity);
    }

    [Fact]
    public void ReceiptRequestCarriesTraceabilityAndLegalEntityScope()
    {
        var entity = Guid.NewGuid();
        var line = new PurchaseReceiptLineRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3m, Guid.NewGuid(), Guid.NewGuid());
        var request = new ReceivePurchaseOrderRequest(entity, 3, "receipt-3", new(2026, 10, 29), new(2026, 10, 29), [line]);

        Assert.Equal(entity, request.LegalEntityId);
        Assert.NotNull(request.Lines[0].LotId);
        Assert.NotNull(request.Lines[0].SerialId);
    }

    [Fact]
    public void MatchingRequestSupportsToleranceAndExceptionApproval()
    {
        var request = new MatchPurchaseOrderRequest(5, Guid.NewGuid(), "ThreeWay", 0.01m, 1m, 2m, 0.5m, "match-5", true, "اعتماد فرق موثق");

        Assert.Equal("ThreeWay", request.Basis);
        Assert.True(request.ApproveException);
        Assert.NotEmpty(request.Reason!);
    }

    [Fact]
    public void SupplierReturnCanPreserveLotSerialAndResolution()
    {
        var request = new CreateSupplierReturnRequest(8, "PhysicalReturn", "تالف عند الاستلام", [new(Guid.NewGuid(), 1m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid())]);

        Assert.Equal("PhysicalReturn", request.Resolution);
        Assert.NotNull(request.Lines[0].LotId);
        Assert.NotNull(request.Lines[0].SerialId);
    }

    [Fact]
    public void PurchaseSettingsExposeIndependentQuantityPriceAndTaxTolerances()
    {
        var entity = Guid.NewGuid();
        var settings = new PurchaseSettingsRequest(entity, true, 10000m, "Tolerance", 2m, false, true, true, 0.5m, 1m, 2m, 0.25m, "Receipt");

        Assert.Equal(entity, settings.LegalEntityId);
        Assert.Equal("Receipt", settings.InvoiceEligibilityBasis);
        Assert.Equal(0.25m, settings.TaxTolerance);
    }

    [Fact]
    public void ReceiveAndBillUsesOneReplayKeyAndPreservesPartialInvoiceLines()
    {
        var lineId = Guid.NewGuid();
        var receipt = new ReceivePurchaseOrderRequest(Guid.NewGuid(), 2, "workflow-2", new(2026, 10, 29), new(2026, 10, 29),
            [new(lineId, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 3m)]);
        var invoice = new CreateSupplierInvoiceRequest(2, "invoice-2", new(2026, 10, 29), new(2026, 10, 29), new(2026, 10, 29), null,
            [new(lineId, 2m)]);

        Assert.Equal("workflow-2", receipt.IdempotencyKey);
        Assert.Equal(invoice.Lines![0].PurchaseOrderLineId, receipt.Lines[0].PurchaseOrderLineId);
        Assert.Equal(2m, invoice.Lines[0].Quantity);
    }

    [Fact]
    public void SupplierCreditCarriesTheOriginalInvoiceAndDoesNotAlterPhysicalResolution()
    {
        var invoiceId = Guid.NewGuid();
        var request = new CreateSupplierReturnRequest(3, "SupplierCredit", "فرق جودة", [], invoiceId);

        Assert.Equal(invoiceId, request.OriginalInvoiceId);
        Assert.Equal("SupplierCredit", request.Resolution);
    }
}
