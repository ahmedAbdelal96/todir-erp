export type QuotationState = "Draft" | "Open" | "Accepted" | "Declined" | "Expired" | "Cancelled" | "Converted";
export type SalesOrderCommercialState = "Draft" | "Confirmed" | "Cancelled";
export type SalesOrderFulfillmentState = "NotRequired" | "Pending" | "PartiallyFulfilled" | "Fulfilled" | "Backordered";
export type SalesOrderInvoicingState = "NotInvoiced" | "PartiallyInvoiced" | "Invoiced";
export type SalesCreditState = "NotChecked" | "Allowed" | "Warn" | "Blocked" | "ApprovalRequired";
export type SalesReturnState = "Requested" | "Approved" | "Executed" | "Rejected" | "Cancelled";

export interface QuotationLineRequest {
  readonly productVariantId?: string | null | undefined;
  readonly unitOfMeasureId?: string | null | undefined;
  readonly description: string;
  readonly quantity: number;
  readonly unitPrice?: number | null | undefined;
  readonly discountAmount?: number | undefined;
  readonly uomCode?: string | null | undefined;
}

export interface CreateQuotationRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly partyId: string;
  readonly currencyCode: string;
  readonly quoteDate: string; // ISO date YYYY-MM-DD
  readonly validUntil?: string | null | undefined;
  readonly lines: readonly QuotationLineRequest[];
  readonly customerReference?: string | null | undefined;
  readonly requestedDeliveryDate?: string | null | undefined;
  readonly salespersonId?: string | null | undefined;
  readonly salesTeamId?: string | null | undefined;
  readonly salesChannelId?: string | null | undefined;
  readonly priceListId?: string | null | undefined;
}

export interface UpdateQuotationRequest {
  readonly version: number;
  readonly partyId: string;
  readonly currencyCode: string;
  readonly quoteDate: string;
  readonly validUntil?: string | null | undefined;
  readonly lines: readonly QuotationLineRequest[];
  readonly customerReference?: string | null | undefined;
  readonly requestedDeliveryDate?: string | null | undefined;
  readonly salespersonId?: string | null | undefined;
  readonly salesTeamId?: string | null | undefined;
  readonly salesChannelId?: string | null | undefined;
  readonly priceListId?: string | null | undefined;
}

export interface AcceptQuotationRequest {
  readonly revisionNumber: number;
  readonly acceptanceMethod: string;
  readonly evidenceReference?: string | null | undefined;
  readonly note?: string | null | undefined;
  readonly idempotencyKey: string;
}

export interface AcceptAndInvoiceQuotationRequest {
  readonly revisionNumber: number;
  readonly acceptanceMethod: string;
  readonly evidenceReference?: string | null | undefined;
  readonly note?: string | null | undefined;
  readonly acceptanceIdempotencyKey: string;
  readonly invoiceIdempotencyKey: string;
  readonly deliverNow?: boolean | undefined;
  readonly warehouseId?: string | null | undefined;
}

export interface ConvertQuotationRequest {
  readonly acceptedRevision: number;
  readonly idempotencyKey: string;
  readonly conversionMode?: string | undefined;
  readonly deliverNow?: boolean | undefined;
  readonly warehouseId?: string | null | undefined;
}

export interface ConvertQuotationToOrderRequest {
  readonly acceptedRevision: number;
  readonly idempotencyKey: string;
  readonly warehouseId?: string | null | undefined;
}

export interface TransitionQuotationRequest {
  readonly version: number;
  readonly reason?: string | null | undefined;
}

export interface CreateOrderRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly partyId: string;
  readonly currencyCode: string;
  readonly orderDate: string;
  readonly quotationId?: string | null | undefined;
  readonly lines: readonly QuotationLineRequest[];
  readonly customerReference?: string | null | undefined;
  readonly requestedDeliveryDate?: string | null | undefined;
  readonly warehouseId?: string | null | undefined;
}

export interface UpdateOrderRequest {
  readonly version: number;
  readonly lines: readonly QuotationLineRequest[];
  readonly customerReference?: string | null | undefined;
  readonly requestedDeliveryDate?: string | null | undefined;
}

export interface OrderCommandRequest {
  readonly version: number;
  readonly idempotencyKey: string;
  readonly reason?: string | null | undefined;
  readonly warehouseId?: string | null | undefined;
  readonly creditOverrideReason?: string | null | undefined;
}

export interface FulfillOrderLineRequest {
  readonly orderLineId: string;
  readonly quantity: number;
  readonly warehouseId?: string | null | undefined;
}

export interface FulfillOrderRequest {
  readonly version: number;
  readonly idempotencyKey: string;
  readonly lines: readonly FulfillOrderLineRequest[];
}

export interface CreateReturnRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly partyId: string;
  readonly sourceDocumentId?: string | null | undefined;
  readonly sourceDocumentType: "SalesOrder" | "Invoice";
  readonly lines: readonly ReturnLineRequest[];
  readonly reason: string;
}

export interface ReturnLineRequest {
  readonly sourceLineId?: string | null | undefined;
  readonly productVariantId?: string | null | undefined;
  readonly quantity: number;
  readonly description: string;
  readonly unitOfMeasureId?: string | null | undefined;
  readonly warehouseId?: string | null | undefined;
  readonly locationId?: string | null | undefined;
  readonly stockStatus?: string | undefined;
  readonly lotId?: string | null | undefined;
  readonly serialId?: string | null | undefined;
}

export interface ReturnCommandRequest {
  readonly version: number;
  readonly idempotencyKey: string;
  readonly reason?: string | null | undefined;
}

export interface SalesCustomerProfileRequest {
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly defaultSalespersonId?: string | null | undefined;
  readonly defaultSalesTeamId?: string | null | undefined;
  readonly defaultSalesChannelId?: string | null | undefined;
  readonly defaultWarehouseId?: string | null | undefined;
  readonly defaultQuoteValidityDays?: number | null | undefined;
  readonly customerPoRequired?: boolean | undefined;
}

export interface QuotationListItem {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly number: string;
  readonly partyId: string;
  readonly quoteDate: string;
  readonly validUntil?: string | null | undefined;
  readonly state: QuotationState;
  readonly currentRevisionNumber: number;
  readonly grossTotal: number;
  readonly currencyCode: string;
  readonly version: number;
}

export interface QuotationLineView {
  readonly id: string;
  readonly sequence: number;
  readonly productVariantId?: string | null | undefined;
  readonly unitOfMeasureId?: string | null | undefined;
  readonly uomCode?: string | null | undefined;
  readonly description: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly discountAmount: number;
  readonly netAmount: number;
}

export interface QuotationAcceptanceView {
  readonly revisionNumber: number;
  readonly acceptedAt: string;
  readonly acceptanceMethod: string;
  readonly recordedBy: string;
  readonly evidenceReference?: string | null | undefined;
  readonly note?: string | null | undefined;
}

export interface SalesHistoryView {
  readonly eventType: string;
  readonly revisionNumber?: number | null | undefined;
  readonly fromState?: string | null | undefined;
  readonly toState?: string | null | undefined;
  readonly occurredAt: string;
  readonly actorId: string;
  readonly reason?: string | null | undefined;
}

export interface QuotationDetail {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly number: string;
  readonly partyId: string;
  readonly quoteDate: string;
  readonly validUntil?: string | null | undefined;
  readonly state: QuotationState;
  readonly currentRevisionNumber: number;
  readonly version: number;
  readonly currencyCode: string;
  readonly customerReference?: string | null | undefined;
  readonly requestedDeliveryDate?: string | null | undefined;
  readonly netTotal: number;
  readonly discountTotal: number;
  readonly grossTotal: number;
  readonly lines: readonly QuotationLineView[];
  readonly acceptances: readonly QuotationAcceptanceView[];
  readonly history: readonly SalesHistoryView[];
}

export interface SalesPagedResult<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface ConversionResult {
  readonly quotationId: string;
  readonly revisionNumber: number;
  readonly invoiceId: string;
  readonly invoiceLifecycle: string;
  readonly warnings: readonly string[];
}

export interface SalesOrderListItem {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly number: string;
  readonly partyId: string;
  readonly commercialState: SalesOrderCommercialState;
  readonly fulfillmentState: SalesOrderFulfillmentState;
  readonly invoicingState: SalesOrderInvoicingState;
  readonly version: number;
  readonly orderedTotal: number;
}

export interface SalesOrderLineView {
  readonly id: string;
  readonly sequence: number;
  readonly productVariantId?: string | null;
  readonly unitOfMeasureId?: string | null;
  readonly description: string;
  readonly orderedQuantity: number;
  readonly fulfilledQuantity: number;
  readonly cancelledQuantity: number;
  readonly invoicedQuantity: number;
  readonly remainingQuantity: number;
  readonly unitPrice: number;
  readonly invoiceableQuantity?: number;
}

export interface SalesOrderDetail {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly number: string;
  readonly partyId: string;
  readonly orderDate: string;
  readonly currencyCode: string;
  readonly warehouseId?: string | null;
  readonly commercialState: SalesOrderCommercialState;
  readonly fulfillmentState: SalesOrderFulfillmentState;
  readonly invoicingState: SalesOrderInvoicingState;
  readonly creditState: SalesCreditState;
  readonly version: number;
  readonly lines: readonly SalesOrderLineView[];
}

export interface SalesReturnView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly state: SalesReturnState;
  readonly sourceDocumentType: "SalesOrder" | "Invoice";
  readonly sourceDocumentId?: string | null;
  readonly reason: string;
  readonly version: number;
  readonly lines: readonly ReturnLineRequest[];
}

export interface SalesReturnExecutionResult {
  readonly returnId: string;
  readonly state: string;
  readonly inventoryReceiptId?: string | null;
  readonly creditNoteId?: string | null;
  readonly version: number;
}
