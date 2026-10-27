namespace Modules.Invoicing.DTOs;

public sealed record InvoiceLineInput(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, string Quantity,
    string? UnitPrice = null, string? DiscountAmount = null, string? SourceLineReference = null, string? PriceOverrideReason = null, string? BusinessCategoryCode = null);
public sealed record InvoiceChargeInput(string Code, string Description, string Amount, string TaxTreatment, string? BusinessCategoryCode = null);
public sealed record CreateInvoiceDraftRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, Guid? AddressId, DateOnly DocumentDate,
    DateOnly PostingDate, DateOnly TaxDate, DateOnly? SupplyDate, string CurrencyCode, Guid? PaymentTermId, string? SupplierInvoiceNumber,
    IReadOnlyList<InvoiceLineInput> Lines, string? DocumentDiscountAmount = null, IReadOnlyList<InvoiceChargeInput>? Charges = null, string PriceMode = "Exclusive");
public sealed record UpdateInvoiceDraftRequest(long Version, Guid PartyId, Guid? AddressId, DateOnly DocumentDate, DateOnly PostingDate,
    DateOnly TaxDate, DateOnly? SupplyDate, string CurrencyCode, Guid? PaymentTermId, string? SupplierInvoiceNumber, IReadOnlyList<InvoiceLineInput> Lines,
    string? DocumentDiscountAmount = null, IReadOnlyList<InvoiceChargeInput>? Charges = null, string PriceMode = "Exclusive");
public sealed record InvoiceListQuery(Guid LegalEntityId, Guid? BranchId = null, Guid? PartyId = null, DateOnly? FromDate = null, DateOnly? ToDate = null,
    string? CurrencyCode = null, string? Lifecycle = null, string? Search = null, int Page = 1, int PageSize = 50);
public sealed record InvoiceDraftCommand(string IdempotencyKey, long Version);
public sealed record CancelInvoiceDraftRequest(long Version, string Reason);
public sealed record InvoiceCorrectionLineInput(Guid OriginalLineId, string Quantity);
public sealed record InvoiceCorrectionRequest(string DocumentType, string Reason, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate,
    DateOnly? SupplyDate, string IdempotencyKey, IReadOnlyList<InvoiceCorrectionLineInput> Lines, string? DocumentDiscountAmount = null,
    IReadOnlyList<InvoiceChargeInput>? Charges = null);
public sealed record InvoiceCorrectionSourceLine(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode,
    string Description, decimal OriginalQuantity, decimal PreviouslyCreditedQuantity, decimal? FinalUnitPrice, decimal LineDiscount,
    string? PriceOverrideReason, string? BusinessCategoryCode);
public sealed record InvoiceCorrectionSource(Guid LegalEntityId, Guid? BranchId, string Direction, Guid PartyId, Guid? AddressId,
    DateOnly OriginalDocumentDate, string CurrencyCode, Guid? PaymentTermId, string PriceMode, string Lifecycle, string DocumentType,
    decimal? GrossTotal, IReadOnlyList<InvoiceCorrectionSourceLine> Lines);
public sealed record InvoiceListItem(Guid Id, string Direction, string DocumentType, string? DocumentNumber, string PartyName, DateOnly DocumentDate,
    string CurrencyCode, string? Total, string Lifecycle, long Version, Guid LegalEntityId, Guid? BranchId);
public sealed record InvoicePage(IReadOnlyList<InvoiceListItem> Items, int TotalCount, int Page, int PageSize);
public sealed record InvoiceLineResult(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description,
    string Quantity, string? ResolvedUnitPrice, string FinalUnitPrice, string NetAmount, string TaxAmount, string GrossAmount, string? PriceExplanation,
    IReadOnlyList<Shared.Contracts.Tax.TaxLineContract>? TaxComponents = null, string? ProductEvidenceJson = null, string? UnitEvidenceJson = null);
public sealed record InvoiceTotals(string NetAmount, string DiscountAmount, string ChargeAmount, string TaxableAmount, string TaxAmount, string GrossAmount,
    string RoundingAdjustment, string CurrencyCode);
public sealed record InvoiceScheduleLine(int Sequence, DateOnly DueDate, string Amount, string CurrencyCode);
public sealed record InvoiceSourceTrace(string Module, Guid DocumentId, string Purpose);
public sealed record InvoiceChargeAllocationDetail(Guid LineId, string Amount);
public sealed record InvoiceChargeDetail(Guid Id, string Code, string Description, string Amount, string TaxTreatment,
    IReadOnlyList<InvoiceChargeAllocationDetail> Allocations);
public sealed record InvoiceDiscountAllocationDetail(Guid LineId, string DiscountType, string SourceAmount, string AllocatedAmount);
public sealed record InvoiceIssue(string Code, string Message, string Severity, string? LineReference = null);
public sealed record InvoicePreview(Guid InvoiceId, long Version, IReadOnlyList<InvoiceLineResult> Lines, InvoiceTotals? Totals,
    IReadOnlyList<InvoiceScheduleLine> PaymentSchedule, IReadOnlyList<InvoiceIssue> Warnings, IReadOnlyList<InvoiceIssue> Blockers, bool ReadyToFinalize,
    Shared.Contracts.Ledger.InvoiceAccountingPreview? AccountingPreview = null);
public sealed record InvoiceFinalizationRequest(long Version, string IdempotencyKey);
public sealed record InvoiceFinalizedResult(Guid InvoiceId, string DocumentNumber, string Lifecycle, InvoiceTotals Totals,
    IReadOnlyList<InvoiceScheduleLine> PaymentSchedule, Guid JournalEntryId, IReadOnlyList<Guid> OpenItemIds, string RegulatoryNextAction);
public sealed record InvoiceDetail(Guid Id, Guid LegalEntityId, Guid? BranchId, string Direction, string DocumentType, string? DocumentNumber,
    Guid PartyId, string PartyDisplayName, string? PartyLegalName, string? PartyTradeName, string CurrencyCode, DateOnly DocumentDate,
    DateOnly PostingDate, DateOnly TaxDate, DateOnly? SupplyDate, Guid? PaymentTermId, string? SupplierInvoiceNumber, string Lifecycle,
    long Version, InvoiceTotals? Totals, IReadOnlyList<InvoiceLineResult> Lines, IReadOnlyList<InvoiceScheduleLine> PaymentSchedule,
    string SettlementSummary, string RegulatorySummary, Guid? JournalEntryId, string DocumentDiscountAmount = "0",
    IReadOnlyList<InvoiceChargeDetail>? Charges = null, IReadOnlyList<InvoiceDiscountAllocationDetail>? DiscountAllocations = null, string PriceMode = "Exclusive",
    string? AddressEvidenceJson = null, InvoiceSourceTrace? Source = null);
public sealed record InvoiceRenderModel(Guid Id, string DocumentNumber, string Direction, string DocumentType, string PartyDisplayName,
    string? PartyLegalName, string? PartyTradeName, string? AddressEvidenceJson, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate,
    DateOnly? SupplyDate, string CurrencyCode, string PriceMode, InvoiceTotals Totals, IReadOnlyList<InvoiceLineResult> Lines,
    IReadOnlyList<InvoiceScheduleLine> PaymentSchedule, string DocumentDiscountAmount, IReadOnlyList<InvoiceChargeDetail> Charges);
public sealed record InvoiceDraftResponse(Guid Id, Guid LegalEntityId, Guid? BranchId, string Direction, Guid PartyId, Guid? AddressId,
    DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, DateOnly? SupplyDate, string CurrencyCode, Guid? PaymentTermId,
    string? SupplierInvoiceNumber, string Lifecycle, long Version, IReadOnlyList<InvoiceLineInput> Lines, string DocumentDiscountAmount,
    IReadOnlyList<InvoiceChargeInput> Charges, string PriceMode = "Exclusive", InvoiceSourceTrace? Source = null);
public sealed record InvoicePriceSnapshotData(Guid LineId, string? ResolvedUnitPrice, string FinalUnitPrice, string EvidenceJson);
public sealed record InvoiceUnitSnapshotData(Guid LineId, Guid? UnitOfMeasureId, string? UnitCode, string? ProductEvidenceJson, string? UnitEvidenceJson);
public sealed record InvoiceTaxSnapshotData(Guid LineId, int Sequence, string TaxCode, string TaxTypeCode, string Treatment, string TaxableBase, string TaxAmount, string EvidenceJson);
public sealed record InvoiceLineAmountSnapshotData(Guid LineId, string NetAmount, string TaxAmount, string GrossAmount);
public sealed record InvoiceChargeSnapshotData(Guid Id, string Code, string Description, string Amount, string TaxTreatment, string? BusinessCategoryCode);
public sealed record InvoiceChargeAllocationSnapshotData(Guid ChargeId, Guid LineId, string Amount);
public sealed record InvoiceFinalizationData(InvoiceTotals Totals, string PartyDisplayName, string PartyEvidenceJson, string? AddressEvidenceJson,
    IReadOnlyList<InvoiceUnitSnapshotData> Units, IReadOnlyList<InvoicePriceSnapshotData> Prices, IReadOnlyList<InvoiceTaxSnapshotData> Taxes,
    IReadOnlyList<InvoiceLineAmountSnapshotData> LineAmounts, IReadOnlyList<InvoiceChargeSnapshotData> Charges,
    IReadOnlyList<InvoiceChargeAllocationSnapshotData> ChargeAllocations, IReadOnlyList<InvoiceDiscountAllocationSnapshotData> DiscountAllocations,
    IReadOnlyList<InvoiceScheduleLine> Schedule, Guid? PaymentTermId, Guid? PaymentTermVersionId, string? PaymentTermEvidenceJson,
    string SettlementSummary, string RegulatorySummary,
    Shared.Contracts.FinanceBase.FinancialCalculationContextSnapshotContract? FinancialContext);
public sealed record InvoiceDiscountAllocationSnapshotData(Guid LineId, string DiscountType, string SourceAmount, string AllocatedAmount);
public sealed record InvoiceFinalizationRecord(string DocumentNumber, Guid JournalEntryId, IReadOnlyList<Guid> OpenItemIds,
    InvoiceTotals Totals, IReadOnlyList<InvoiceScheduleLine> Schedule, bool AlreadyFinalized);
