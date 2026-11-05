using Shared.Primitives.Identifiers;

namespace Todir.Sales.Contracts;

public sealed record SalesSourceReference(Guid QuotationId, int RevisionNumber, Guid InvoiceId, string Purpose);
public sealed record SalesCommercialLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string Description, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, string? UomCode);
public sealed record SalesConversionRequest(Guid QuotationId, int AcceptedRevision, string ConversionMode, bool DeliverNow, Guid? WarehouseId, string IdempotencyKey);
public sealed record SalesCreditDecision(string Decision, string PolicyCode, string? AvailableCredit, string? ProposedExposure, long ExposureVersion, string? Reason);
public sealed record SalesQuotationSourceContract(Guid LegalEntityId, Guid PartyId, string CurrencyCode, DateOnly QuoteDate, DateOnly? ValidUntil, IReadOnlyList<SalesCommercialLine> Lines, string? CustomerReference, string IdempotencyKey);
public interface ISalesQuotationCapabilities
{
    Task<Guid> CreateDraftQuotationAsync(TenantId tenantId, Guid actorId, SalesQuotationSourceContract request, CancellationToken cancellationToken = default);
}
