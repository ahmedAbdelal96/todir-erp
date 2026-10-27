namespace Modules.Invoicing.Entities;

using Shared.Contracts.Invoicing;

public enum InvoiceDirection { Receivable, Payable }
public enum InvoiceDocumentType { Invoice, CreditNote, DebitNote }
public enum InvoiceLifecycle { Draft, Finalized, CorrectedOrCancelled, CancelledDraft }

public sealed class InvoiceException(string code, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed record InvoiceLineDraft(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description,
    decimal Quantity, string? ExplicitUnitPrice, decimal DiscountAmount, string? SourceLineReference, string? PriceOverrideReason, string? BusinessCategoryCode);
public sealed record InvoiceChargeDraft(Guid Id, string Code, string Description, decimal Amount, string TaxTreatment, string? BusinessCategoryCode);

public sealed class InvoiceAggregate
{
    private readonly List<InvoiceLineDraft> lines;
    private readonly List<InvoiceChargeDraft> charges = [];

    public InvoiceAggregate(Guid id, Guid tenantId, Guid legalEntityId, Guid? branchId, InvoiceDirection direction, InvoiceDocumentType documentType,
        Guid partyId, Guid? addressId, DateOnly documentDate, DateOnly postingDate, DateOnly taxDate, DateOnly? supplyDate, string currencyCode,
        Guid? paymentTermId, string? supplierInvoiceNumber, long version, InvoiceLifecycle lifecycle, IReadOnlyList<InvoiceLineDraft> lines,
        decimal documentDiscountAmount = 0m, IReadOnlyList<InvoiceChargeDraft>? charges = null, string priceMode = "Exclusive",
        InvoiceSourceReferenceContract? source = null)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || legalEntityId == Guid.Empty || partyId == Guid.Empty) throw new InvoiceException("invoicing.identity_invalid", "Invoice identity is incomplete.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3) throw new InvoiceException("invoicing.currency_invalid", "A three-letter transaction currency is required.");
        if (priceMode is not ("Exclusive" or "Inclusive")) throw new InvoiceException("invoicing.price_mode_invalid", "Invoice prices must be tax-exclusive or tax-inclusive.");
        if (lines.Count is < 1 or > 1000) throw new InvoiceException("invoicing.lines_invalid", "An invoice must contain between one and 1000 lines.");
        if (lines.Any(line => line.Quantity <= 0 || string.IsNullOrWhiteSpace(line.Description) || line.DiscountAmount < 0)) throw new InvoiceException("invoicing.line_invalid", "Invoice lines require a description, positive quantity, and non-negative discount.");
        if (documentDiscountAmount < 0 || (charges?.Count ?? 0) > 100 || charges?.Any(charge => charge.Amount <= 0 || string.IsNullOrWhiteSpace(charge.Code) || string.IsNullOrWhiteSpace(charge.Description)) == true)
            throw new InvoiceException("invoicing.commercial_adjustment_invalid", "Document discounts and typed charges must be valid positive amounts.");
        if (lines.Where(line => line.ExplicitUnitPrice is not null).Any(line => !decimal.TryParse(line.ExplicitUnitPrice, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0))
            throw new InvoiceException("invoicing.price_invalid", "An explicit unit price must be a non-negative decimal amount.");
        Id = id; TenantId = tenantId; LegalEntityId = legalEntityId; BranchId = branchId; Direction = direction; DocumentType = documentType;
        PartyId = partyId; AddressId = addressId; DocumentDate = documentDate; PostingDate = postingDate; TaxDate = taxDate; SupplyDate = supplyDate;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant(); PaymentTermId = paymentTermId; SupplierInvoiceNumber = supplierInvoiceNumber?.Trim();
        Version = version; Lifecycle = lifecycle; this.lines = [.. lines.OrderBy(line => line.Sequence)];
        DocumentDiscountAmount = documentDiscountAmount; this.charges = [.. (charges ?? []).OrderBy(charge => charge.Code, StringComparer.Ordinal)];
        PriceMode = priceMode;
        Source = source;
        if (this.lines.Select(line => line.Sequence).Distinct().Count() != this.lines.Count) throw new InvoiceException("invoicing.line_sequence_invalid", "Invoice line sequence values must be unique.");
    }

    public Guid Id { get; }
    public Guid TenantId { get; }
    public Guid LegalEntityId { get; }
    public Guid? BranchId { get; }
    public InvoiceDirection Direction { get; }
    public InvoiceDocumentType DocumentType { get; }
    public Guid PartyId { get; }
    public Guid? AddressId { get; }
    public DateOnly DocumentDate { get; }
    public DateOnly PostingDate { get; }
    public DateOnly TaxDate { get; }
    public DateOnly? SupplyDate { get; }
    public string CurrencyCode { get; }
    public Guid? PaymentTermId { get; }
    public string? SupplierInvoiceNumber { get; }
    public decimal DocumentDiscountAmount { get; }
    public string PriceMode { get; }
    public InvoiceSourceReferenceContract? Source { get; }
    public long Version { get; }
    public InvoiceLifecycle Lifecycle { get; }
    public IReadOnlyList<InvoiceLineDraft> Lines => lines.AsReadOnly();
    public IReadOnlyList<InvoiceChargeDraft> Charges => charges.AsReadOnly();

    public void EnsureDraftVersion(long expectedVersion)
    {
        if (Lifecycle != InvoiceLifecycle.Draft) throw new InvoiceException("invoicing.already_finalized", "A finalized invoice cannot be changed.");
        if (expectedVersion != Version) throw new InvoiceException("invoicing.draft_changed", "The invoice changed after it was loaded. Refresh and try again.");
    }
}
