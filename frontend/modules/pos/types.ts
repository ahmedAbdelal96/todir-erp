// POS Module 20 Types & Contracts

export type PosRegisterStatus = "Active" | "Disabled" | "Maintenance";
export type PosTerminalStatus = "Ready" | "Active" | "Suspended" | "Revoked";
export type PosSessionStatus = "Open" | "Reconciling" | "Closed";
export type PosCartState = "Active" | "Held" | "Cancelled" | "CheckedOut";
export type PosCheckoutState = "Pending" | "Completed" | "Failed" | "Cancelled";
export type PosTenderMethod = "Cash" | "Card" | "CustomerCredit" | "ExternalTerminal";
export type CashMovementType = "CashIn" | "CashOut" | "Drop";

export interface RegisterResponse {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId: string;
  readonly code: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly status: PosRegisterStatus;
  readonly offlineEnabled: boolean;
}

export interface CreateRegisterRequest {
  readonly legalEntityId: string;
  readonly branchId: string;
  readonly code: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly offlineEnabled?: boolean;
}

export interface UpdateRegisterRequest {
  readonly name: string;
  readonly currencyCode: string;
  readonly offlineEnabled: boolean;
}

export interface TerminalResponse {
  readonly id: string;
  readonly registerId: string;
  readonly deviceIdentity: string;
  readonly displayName: string;
  readonly status: PosTerminalStatus;
  readonly credentialVersion: number;
  readonly lastSeenAt: string | null;
}

export interface RegisterTerminalRequest {
  readonly registerId: string;
  readonly deviceIdentity: string;
  readonly displayName: string;
}

export interface SessionResponse {
  readonly id: string;
  readonly registerId: string;
  readonly terminalId: string | null;
  readonly businessDate: string;
  readonly status: PosSessionStatus;
  readonly openingFloat: number;
  readonly countedCash: number | null;
  readonly variance: number | null;
}

export interface OpenSessionRequest {
  readonly registerId: string;
  readonly terminalId?: string | null;
  readonly cashierUserId: string;
  readonly businessDate: string;
  readonly openingFloat: number;
  readonly currencyCode: string;
}

export interface CloseSessionRequest {
  readonly sessionId: string;
  readonly countedCash: number;
  readonly varianceReason?: string | null | undefined;
}

export interface CashMovementRequest {
  readonly sessionId: string;
  readonly movementType: CashMovementType;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reason: string;
}

export interface CartResponse {
  readonly id: string;
  readonly sessionId: string;
  readonly state: PosCartState;
  readonly version: number;
  readonly subtotal: number;
  readonly taxTotal: number;
  readonly discountTotal: number;
  readonly grandTotal: number;
}

export interface CartLineResponse {
  readonly id: string;
  readonly lineNo: number;
  readonly variantId: string;
  readonly description: string;
  readonly uomCode: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly taxAmount: number;
  readonly discountAmount: number;
  readonly lineTotal: number;
}

export interface CartDetailsResponse {
  readonly cart: CartResponse;
  readonly lines: readonly CartLineResponse[];
}

export interface CreateCartRequest {
  readonly sessionId: string;
  readonly currencyCode: string;
  readonly customerPartyId?: string | null | undefined;
}

export interface AddCartLineRequest {
  readonly variantId: string;
  readonly description: string;
  readonly uomCode: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly taxAmount?: number;
  readonly discountAmount?: number;
  readonly expectedVersion?: number | null;
  readonly legalEntityId?: string | null;
  readonly productTemplateId?: string | null;
  readonly unitOfMeasureId?: string | null;
  readonly priceListId?: string | null;
  readonly pricingDate?: string | null;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface ScanCartLineRequest {
  readonly skuOrBarcode: string;
  readonly quantity: number;
  readonly legalEntityId: string;
  readonly priceListId: string;
  readonly uomCode?: string | null;
  readonly discountAmount?: number;
  readonly expectedVersion?: number | null;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface UpdateCartLineRequest {
  readonly quantity: number;
  readonly discountAmount: number;
  readonly expectedVersion: number;
}

export interface DiscountCartRequest {
  readonly lineId: string;
  readonly amount: number;
  readonly expectedVersion: number;
}

export interface SetCartCustomerRequest {
  readonly customerPartyId: string | null;
}

export interface TenderRequest {
  readonly method: PosTenderMethod;
  readonly amount: number;
  readonly externalReference?: string | null;
  readonly paymentAccountId?: string | null;
  readonly providerConfigId?: string | null;
}

export interface CheckoutRequest {
  readonly cartId: string;
  readonly idempotencyKey: string;
  readonly tenders: readonly TenderRequest[];
}

export interface CheckoutForCartRequest {
  readonly idempotencyKey: string;
  readonly tenders: readonly TenderRequest[];
}

export interface CheckoutResponse {
  readonly checkoutId: string;
  readonly saleId: string | null;
  readonly state: PosCheckoutState;
  readonly paymentState: string;
  readonly inventoryState: string;
  readonly documentState: string;
  readonly accountingState: string;
  readonly total: number;
  readonly currencyCode: string;
}

export interface CatalogVariantSnapshot {
  readonly variantId: string;
  readonly templateId: string;
  readonly sku: string;
  readonly barcode: string;
  readonly displayName: string;
  readonly uomCode: string;
  readonly uomId: string;
  readonly unitPrice: number;
  readonly isStocked: boolean;
  readonly isWeighable?: boolean;
}

export interface CatalogSearchResult {
  readonly items: readonly CatalogVariantSnapshot[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface ReturnLineRequest {
  readonly originalSaleLineId: string;
  readonly quantity: number;
  readonly amount: number;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface CreateReturnRequest {
  readonly originalSaleId: string;
  readonly sessionId: string;
  readonly lines: readonly ReturnLineRequest[];
  readonly reason: string;
  readonly disposition: "ReturnToStock" | "Scrap";
}

export interface RefundReturnRequest {
  readonly paymentAccountId: string;
  readonly amount: number;
  readonly reasonCode?: string;
}

export interface ExchangeRequest {
  readonly return: CreateReturnRequest;
  readonly checkout: CheckoutRequest;
}

export interface ExchangeResponse {
  readonly exchangeId: string;
  readonly returnId: string;
  readonly checkoutId: string | null;
  readonly saleId: string | null;
  readonly state: string;
  readonly differenceAmount: number;
}

export interface CompleteExchangeRequest {
  readonly checkout: CheckoutRequest;
}

export interface ManagerOverrideRequest {
  readonly scope: string;
  readonly targetId: string;
  readonly reason: string;
  readonly expiresAt: string;
}

export interface OfflineEnvelopeRequest {
  readonly registerId: string;
  readonly deviceIdentity: string;
  readonly sourceSequence: number;
  readonly idempotencyKey: string;
  readonly packageVersion: number;
  readonly payload: string;
  readonly signature: string;
}

export interface OfflinePackageResponse {
  readonly packageId: string;
  readonly registerId: string;
  readonly version: number;
  readonly validFrom: string;
  readonly expiresAt: string;
  readonly payload: string;
  readonly signature: string;
}

export interface OfflineConflictResolutionRequest {
  readonly decision: string;
  readonly note: string;
}

export interface OfflineCashSaleLine {
  readonly variantId: string;
  readonly description: string;
  readonly uomCode: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly taxAmount: number;
  readonly discountAmount: number;
  readonly legalEntityId: string;
  readonly productTemplateId: string;
  readonly unitOfMeasureId: string;
  readonly priceListId: string;
  readonly pricingDate: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface OfflineCashSalePayload {
  readonly schemaVersion: number;
  readonly type: string;
  readonly sessionId: string;
  readonly currencyCode: string;
  readonly paymentAccountId: string;
  readonly idempotencyKey: string;
  readonly lines: readonly OfflineCashSaleLine[];
  readonly customerPartyId?: string | null;
}

export interface PosSaleItemReport {
  readonly saleId: string;
  readonly saleNumber: string;
  readonly registerCode: string;
  readonly cashierName: string;
  readonly customerName?: string | null;
  readonly total: number;
  readonly currencyCode: string;
  readonly createdAt: string;
  readonly state: string;
}

export interface PosReturnItemReport {
  readonly returnId: string;
  readonly originalSaleId: string;
  readonly registerCode: string;
  readonly reason: string;
  readonly total: number;
  readonly currencyCode: string;
  readonly createdAt: string;
  readonly state: string;
}

export interface PosPaymentMixReport {
  readonly cashTotal: number;
  readonly cardTotal: number;
  readonly creditTotal: number;
  readonly grandTotal: number;
  readonly transactionCount: number;
  readonly currencyCode: string;
}

export interface PosCashDifferenceReport {
  readonly sessionId: string;
  readonly businessDate: string;
  readonly status: string;
  readonly expectedCash: number;
  readonly countedCash: number | null;
  readonly variance: number;
  readonly reason: string;
  readonly approved: boolean;
}

export interface PosDiscountSummaryReport {
  readonly lineDiscountsTotal: number;
  readonly cartDiscountsTotal: number;
  readonly totalDiscounts: number;
  readonly discountedTransactionsCount: number;
}
