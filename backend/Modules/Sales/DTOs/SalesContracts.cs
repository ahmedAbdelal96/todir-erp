namespace Modules.Sales.DTOs;

public sealed record QuotationLineRequest(Guid? ProductVariantId, Guid? UnitOfMeasureId, string Description, decimal Quantity, decimal? UnitPrice = null, decimal DiscountAmount = 0, string? UomCode = null);
public sealed record CreateQuotationRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, string CurrencyCode, DateOnly QuoteDate, DateOnly? ValidUntil, IReadOnlyList<QuotationLineRequest> Lines, string? CustomerReference = null, DateOnly? RequestedDeliveryDate = null, Guid? SalespersonId = null, Guid? SalesTeamId = null, Guid? SalesChannelId = null, Guid? PriceListId = null);
public sealed record UpdateQuotationRequest(long Version, Guid PartyId, string CurrencyCode, DateOnly QuoteDate, DateOnly? ValidUntil, IReadOnlyList<QuotationLineRequest> Lines, string? CustomerReference = null, DateOnly? RequestedDeliveryDate = null, Guid? SalespersonId = null, Guid? SalesTeamId = null, Guid? SalesChannelId = null, Guid? PriceListId = null);
public sealed record AcceptQuotationRequest(int RevisionNumber, string AcceptanceMethod, string? EvidenceReference, string? Note, string IdempotencyKey);
public sealed record AcceptAndInvoiceQuotationRequest(int RevisionNumber, string AcceptanceMethod, string? EvidenceReference, string? Note, string AcceptanceIdempotencyKey, string InvoiceIdempotencyKey, bool DeliverNow = false, Guid? WarehouseId = null);
public sealed record ConvertQuotationRequest(int AcceptedRevision, string IdempotencyKey, string ConversionMode = "CreateInvoiceDraft", bool DeliverNow = false, Guid? WarehouseId = null);
public sealed record ConvertQuotationToOrderRequest(int AcceptedRevision, string IdempotencyKey, Guid? WarehouseId = null);
public sealed record TransitionQuotationRequest(long Version, string? Reason = null);
public sealed record CreateOrderRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, string CurrencyCode, DateOnly OrderDate, Guid? QuotationId, IReadOnlyList<QuotationLineRequest> Lines, string? CustomerReference = null, DateOnly? RequestedDeliveryDate = null, Guid? WarehouseId = null);
public sealed record UpdateOrderRequest(long Version, IReadOnlyList<QuotationLineRequest> Lines, string? CustomerReference = null, DateOnly? RequestedDeliveryDate = null);
public sealed record OrderCommandRequest(long Version, string IdempotencyKey, string? Reason = null, Guid? WarehouseId = null, string? CreditOverrideReason = null);
public sealed record FulfillOrderLineRequest(Guid OrderLineId, decimal Quantity, Guid? WarehouseId = null);
public sealed record FulfillOrderRequest(long Version, string IdempotencyKey, IReadOnlyList<FulfillOrderLineRequest> Lines);
public sealed record CreateReturnRequest(Guid LegalEntityId, Guid? BranchId, Guid PartyId, Guid? SourceDocumentId, string SourceDocumentType, IReadOnlyList<ReturnLineRequest> Lines, string Reason);
public sealed record ReturnLineRequest(Guid? SourceLineId, Guid? ProductVariantId, decimal Quantity, string Description);
public sealed record ReturnCommandRequest(long Version, string IdempotencyKey, string? Reason = null);
public sealed record SalesCustomerProfileRequest(Guid LegalEntityId, Guid PartyId, Guid? DefaultSalespersonId = null, Guid? DefaultSalesTeamId = null, Guid? DefaultSalesChannelId = null, Guid? DefaultWarehouseId = null, int? DefaultQuoteValidityDays = null, bool CustomerPoRequired = false);
public sealed record SalesChannelRequest(Guid LegalEntityId, string Code, string Name);
public sealed record SalesTeamRequest(Guid LegalEntityId, string Code, string Name);

public sealed record QuotationListItem(Guid Id, Guid LegalEntityId, Guid? BranchId, string Number, Guid PartyId, DateOnly QuoteDate, DateOnly? ValidUntil, string State, int CurrentRevisionNumber, decimal GrossTotal, string CurrencyCode, long Version);
public sealed record QuotationLineView(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UomCode, string Description, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal NetAmount);
public sealed record QuotationDetail(Guid Id, Guid LegalEntityId, Guid? BranchId, string Number, Guid PartyId, DateOnly QuoteDate, DateOnly? ValidUntil, string State, int CurrentRevisionNumber, long Version, string CurrencyCode, string? CustomerReference, DateOnly? RequestedDeliveryDate, decimal NetTotal, decimal DiscountTotal, decimal GrossTotal, IReadOnlyList<QuotationLineView> Lines, IReadOnlyList<QuotationAcceptanceView> Acceptances, IReadOnlyList<SalesHistoryView> History);
public sealed record QuotationAcceptanceView(int RevisionNumber, DateTimeOffset AcceptedAt, string AcceptanceMethod, Guid RecordedBy, string? EvidenceReference, string? Note);
public sealed record SalesHistoryView(string EventType, int? RevisionNumber, string? FromState, string? ToState, DateTimeOffset OccurredAt, Guid ActorId, string? Reason);
public sealed record SalesPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record ConversionResult(Guid QuotationId, int RevisionNumber, Guid InvoiceId, string InvoiceLifecycle, IReadOnlyList<string> Warnings);
public sealed record SalesOrderListItem(Guid Id, Guid LegalEntityId, Guid? BranchId, string Number, Guid PartyId, string CommercialState, string FulfillmentState, string InvoicingState, long Version, decimal OrderedTotal);
public sealed record SalesOrderLineView(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string Description, decimal OrderedQuantity, decimal FulfilledQuantity, decimal CancelledQuantity, decimal InvoicedQuantity, decimal RemainingQuantity, decimal UnitPrice)
{
    public decimal InvoiceableQuantity => OrderedQuantity - InvoicedQuantity - CancelledQuantity;
}
public sealed record SalesOrderDetail(Guid Id, Guid LegalEntityId, Guid? BranchId, string Number, Guid PartyId, DateOnly OrderDate, string CurrencyCode, Guid? WarehouseId, string CommercialState, string FulfillmentState, string InvoicingState, string CreditState, long Version, IReadOnlyList<SalesOrderLineView> Lines);
public sealed record SalesReservationView(Guid OrderLineId, Guid InventoryReservationId, decimal Quantity, string State);
public sealed record SalesFulfillmentResult(Guid FulfillmentId, IReadOnlyList<SalesInventoryFulfillmentLine> Lines, string State);
public sealed record SalesInventoryFulfillmentLine(Guid OrderLineId, decimal RequestedQuantity, decimal FulfilledQuantity, Guid? InventoryOperationId);
public sealed record SalesReturnView(Guid Id, Guid LegalEntityId, Guid PartyId, string State, string SourceDocumentType, Guid? SourceDocumentId, string Reason, long Version, IReadOnlyList<ReturnLineRequest> Lines);
