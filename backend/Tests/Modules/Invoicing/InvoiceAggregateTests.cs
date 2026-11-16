using Modules.Invoicing.Entities;
using Modules.Invoicing.Services;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Invoicing;
using Xunit;

namespace Modules.Invoicing.Tests;

public sealed class InvoiceAggregateTests
{
    private static readonly decimal[] EqualBasis = [1m, 1m, 1m];
    private static readonly decimal[] ZeroBasis = [0m, 0m];
    private static readonly decimal[] ExpectedAllocation = [0.33m, 0.33m, 0.34m];
    [Fact]
    public void DraftAcceptsExactPositiveQuantitiesAndEnforcesOptimisticVersion()
    {
        var invoice = Create([new(Guid.CreateVersion7(), 1, null, null, "EA", "Consulting", 1.25m, "100.50", 0m, null, "Approved exception", null)]);

        invoice.EnsureDraftVersion(1);

        Assert.Equal(InvoiceLifecycle.Draft, invoice.Lifecycle);
        Assert.Equal(1.25m, invoice.Lines[0].Quantity);
    }

    [Fact]
    public void DraftRejectsEmptyOrInvalidLines()
    {
        Assert.Equal("invoicing.lines_invalid", Assert.Throws<InvoiceException>(() => Create([])).Code);
        Assert.Equal("invoicing.line_invalid", Assert.Throws<InvoiceException>(() => Create([new(Guid.CreateVersion7(), 1, null, null, "EA", " ", 1m, null, 0m, null, null, null)])).Code);
        Assert.Equal("invoicing.price_invalid", Assert.Throws<InvoiceException>(() => Create([new(Guid.CreateVersion7(), 1, null, null, "EA", "Service", 1m, "-1", 0m, null, null, null)])).Code);
    }

    [Fact]
    public void DraftRequiresUniqueLineSequencesAndRejectsStaleVersions()
    {
        var duplicate = Assert.Throws<InvoiceException>(() => Create([
            new(Guid.CreateVersion7(), 1, null, null, "EA", "First", 1m, null, 0m, null, null, null),
            new(Guid.CreateVersion7(), 1, null, null, "EA", "Second", 1m, null, 0m, null, null, null)]));
        Assert.Equal("invoicing.line_sequence_invalid", duplicate.Code);

        var invoice = Create([new(Guid.CreateVersion7(), 1, null, null, "EA", "Service", 1m, null, 0m, null, null, null)]);
        Assert.Equal("invoicing.draft_changed", Assert.Throws<InvoiceException>(() => invoice.EnsureDraftVersion(0)).Code);
    }

    [Fact]
    public void FinalizedDocumentsCannotBeEdited()
    {
        var invoice = new InvoiceAggregate(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, InvoiceDirection.Payable,
            InvoiceDocumentType.Invoice, Guid.CreateVersion7(), null, new(2026, 1, 1), new(2026, 1, 1), new(2026, 1, 1), null, "EGP", null, "SUP-1", 2,
            InvoiceLifecycle.Finalized, [new(Guid.CreateVersion7(), 1, null, null, "EA", "Materials", 1m, null, 0m, null, null, null)]);

        Assert.Equal("invoicing.already_finalized", Assert.Throws<InvoiceException>(() => invoice.EnsureDraftVersion(2)).Code);
    }

    [Fact]
    public void DocumentAdjustmentsUseFinBasePrecisionAndExactDeterministicResidual()
    {
        var first = InvoiceCommercialAllocation.Allocate(1m, EqualBasis, 2, FinancialRoundingMode.HalfEven);
        var second = InvoiceCommercialAllocation.Allocate(1m, EqualBasis, 2, FinancialRoundingMode.HalfEven);

        Assert.Equal(ExpectedAllocation, first);
        Assert.Equal(first, second);
        Assert.Equal(1m, first.Sum());
    }

    [Fact]
    public void DocumentAdjustmentRequiresPositiveEligibleBasis()
    {
        var error = Assert.Throws<InvoiceException>(() => InvoiceCommercialAllocation.Allocate(1m, ZeroBasis, 2, FinancialRoundingMode.HalfEven));

        Assert.Equal("invoicing.allocation_basis_missing", error.Code);
    }

    [Fact]
    public void DocumentAdjustmentRoundingNeverCreatesNegativeAllocations()
    {
        var allocation = InvoiceCommercialAllocation.Allocate(0.005m, [1m, 1m], 2, FinancialRoundingMode.HalfUp);

        Assert.All(allocation, amount => Assert.True(amount >= 0m));
        Assert.Equal(0.005m, allocation.Sum());
    }

    [Fact]
    public void SourceContractMapsApprovedSalesAndPurchasingDirectionsDeterministically()
    {
        var sales = InvoiceSourceRequestMapper.Map(Source("sales", "Receivable"));
        var purchasingRequest = Source("purchasing", "Payable");
        var purchasing = InvoiceSourceRequestMapper.Map(purchasingRequest);
        var replay = InvoiceSourceRequestMapper.Map(purchasingRequest);

        Assert.Equal(InvoiceDirection.Receivable, sales.Direction);
        Assert.Equal("sales", sales.Reference.SourceModule);
        Assert.Equal(InvoiceDirection.Payable, purchasing.Direction);
        Assert.Equal(purchasing.Reference.PayloadHash, replay.Reference.PayloadHash);
        Assert.Equal(purchasing.Reference.IdempotencyKey, replay.Reference.IdempotencyKey);
        Assert.Equal("PO-44:1", purchasing.Draft.Lines[0].SourceLineReference);
    }

    [Theory]
    [InlineData("sales", "Payable")]
    [InlineData("purchasing", "Receivable")]
    [InlineData("invoicing", "Receivable")]
    public void SourceContractRejectsSpoofedOrCrossDirectionSources(string module, string direction)
    {
        var error = Assert.Throws<InvoiceException>(() => InvoiceSourceRequestMapper.Map(Source(module, direction)));

        Assert.Equal(module is "sales" or "purchasing" ? "invoicing.source_direction_invalid" : "invoicing.source_reference_invalid", error.Code);
    }

    [Fact]
    public void ServiceSourceCreatesReceivableDraftContract()
    {
        var mapped = InvoiceSourceRequestMapper.Map(Source("service", "Receivable"));
        Assert.Equal(InvoiceDirection.Receivable, mapped.Direction);
        Assert.Equal("service", mapped.Reference.SourceModule);
    }

    [Theory]
    [InlineData(InvoiceDirection.Receivable, InvoiceDocumentType.Invoice, "CustomerInvoicePosted")]
    [InlineData(InvoiceDirection.Receivable, InvoiceDocumentType.CreditNote, "CustomerCreditNotePosted")]
    [InlineData(InvoiceDirection.Receivable, InvoiceDocumentType.DebitNote, "CustomerDebitNotePosted")]
    [InlineData(InvoiceDirection.Payable, InvoiceDocumentType.Invoice, "VendorBillPosted")]
    [InlineData(InvoiceDirection.Payable, InvoiceDocumentType.CreditNote, "VendorCreditNotePosted")]
    [InlineData(InvoiceDirection.Payable, InvoiceDocumentType.DebitNote, "VendorDebitNotePosted")]
    public void AccountingFactTypeFollowsInvoiceDirectionAndDocumentType(InvoiceDirection direction, InvoiceDocumentType documentType, string expected)
    {
        Assert.Equal(expected, InvoiceAccountingFactTypeResolver.Resolve(direction, documentType));
    }

    private static InvoiceAggregate Create(IReadOnlyList<InvoiceLineDraft> lines) => new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null,
        InvoiceDirection.Receivable, InvoiceDocumentType.Invoice, Guid.CreateVersion7(), null, new(2026, 1, 1), new(2026, 1, 1), new(2026, 1, 1), null,
        "EGP", null, null, 1, InvoiceLifecycle.Draft, lines);

    private static InvoiceSourceDocumentContract Source(string module, string direction) => new(module, Guid.Parse("019b2e1a-0000-7000-8000-000000000001"), "Invoice", "sales-order-44",
        direction, Guid.Parse("019b2e1a-0000-7000-8000-000000000002"), null, Guid.Parse("019b2e1a-0000-7000-8000-000000000003"), null,
        new(2026, 1, 1), new(2026, 1, 1), new(2026, 1, 1), null, "EGP", "Exclusive", null, null, null,
        [new(null, null, "EA", "Service", "1", "10", SourceLineReference: "PO-44:1")]);
}
