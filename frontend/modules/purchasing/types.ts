export interface PurchaseOrderLineRequest {
  productVariantId?: string | null;
  unitOfMeasureId?: string | null;
  unitCode?: string | null;
  description: string;
  quantity: number;
  unitPrice?: number | null;
  priceOverrideReason?: string | null;
  productTemplateId?: string | null;
}

export interface CreatePurchaseOrderRequest {
  legalEntityId: string;
  branchId?: string | null;
  supplierPartyId: string;
  currencyCode: string;
  orderDate: string;
  priceListId?: string | null;
  approvalRequired?: boolean | null;
  lines: PurchaseOrderLineRequest[];
}

export interface UpdatePurchaseOrderRequest {
  version: number;
  supplierPartyId: string;
  currencyCode: string;
  orderDate: string;
  lines: PurchaseOrderLineRequest[];
}

export interface PurchaseOrderCommandRequest {
  version: number;
  idempotencyKey: string;
  reason?: string | null;
}

export interface PurchaseInvoiceLineRequest {
  purchaseOrderLineId: string;
  quantity: number;
}

export interface CreateSupplierInvoiceRequest {
  version: number;
  idempotencyKey: string;
  documentDate: string;
  postingDate: string;
  taxDate: string;
  supplierInvoiceNumber?: string | null;
  lines?: PurchaseInvoiceLineRequest[] | null;
}

export interface SupplierPurchaseProfileRequest {
  legalEntityId: string;
  supplierPartyId: string;
  supplierCode?: string | null;
  paymentTermId?: string | null;
  defaultCurrencyCode?: string | null;
  defaultPriceListId?: string | null;
  defaultBuyerId?: string | null;
  defaultWarehouseId?: string | null;
  leadTimeDays?: number | null;
  supplierReferenceRequired: boolean;
  purchasePriceContextJson?: string | null;
}

export interface SupplierPurchaseProfileView {
  legalEntityId: string;
  supplierPartyId: string;
  supplierCode?: string | null;
  paymentTermId?: string | null;
  defaultCurrencyCode?: string | null;
  defaultPriceListId?: string | null;
  defaultBuyerId?: string | null;
  defaultWarehouseId?: string | null;
  leadTimeDays?: number | null;
  supplierReferenceRequired: boolean;
  version: number;
}

export interface PurchaseSettingsRequest {
  legalEntityId: string;
  approvalRequired: boolean;
  approvalThreshold?: number | null;
  overReceiptPolicy: "Disallow" | "Tolerance" | "Approval" | string;
  overReceiptTolerance: number;
  invoiceWithoutReceipt: boolean;
  threeWayMatching: boolean;
  buyerApproverSeparation: boolean;
  quantityTolerance: number;
  priceAbsoluteTolerance: number;
  pricePercentageTolerance: number;
  taxTolerance: number;
  invoiceEligibilityBasis: "PO" | "Receipt" | string;
}

export interface PurchaseSettingsView {
  legalEntityId: string;
  approvalRequired: boolean;
  approvalThreshold?: number | null;
  overReceiptPolicy: string;
  overReceiptTolerance: number;
  invoiceWithoutReceipt: boolean;
  threeWayMatching: boolean;
  buyerApproverSeparation: boolean;
  quantityTolerance: number;
  priceAbsoluteTolerance: number;
  pricePercentageTolerance: number;
  taxTolerance: number;
  invoiceEligibilityBasis: string;
}

export interface PurchaseReceiptLineRequest {
  purchaseOrderLineId: string;
  productVariantId: string;
  warehouseId: string;
  locationId?: string | null;
  unitOfMeasureId: string;
  quantity: number;
  lotId?: string | null;
  serialId?: string | null;
}

export interface ReceivePurchaseOrderRequest {
  legalEntityId: string;
  version: number;
  idempotencyKey: string;
  operationalDate: string;
  postingDate: string;
  lines: PurchaseReceiptLineRequest[];
}

export interface PurchaseReceiptResult {
  purchaseOrderId: string;
  receiptIds: string[];
  version: number;
  receiptState: string;
}

export interface MatchPurchaseOrderRequest {
  version: number;
  supplierInvoiceId: string;
  basis: "TwoWay" | "ThreeWay" | "Disabled" | string;
  quantityTolerance: number;
  priceAbsoluteTolerance: number;
  pricePercentageTolerance: number;
  taxTolerance: number;
  idempotencyKey: string;
  approveException?: boolean;
  reason?: string | null;
}

export interface PurchaseMatchView {
  id: string;
  purchaseOrderId: string;
  supplierInvoiceId: string;
  basis: string;
  state: "Matched" | "WithinTolerance" | "Variance" | "Blocked" | "ApprovedException" | string;
  varianceAmount: number;
  sourceVersion: number;
  detailsJson: string;
}

export interface SupplierReturnLineRequest {
  purchaseOrderLineId: string;
  quantity: number;
  productVariantId?: string | null;
  unitOfMeasureId?: string | null;
  warehouseId?: string | null;
  locationId?: string | null;
  lotId?: string | null;
  serialId?: string | null;
}

export interface CreateSupplierReturnRequest {
  version: number;
  resolution: "PhysicalReturn" | "SupplierCredit" | "ServiceCredit" | string;
  reason: string;
  lines: SupplierReturnLineRequest[];
  originalInvoiceId?: string | null;
}

export interface SupplierReturnView {
  id: string;
  purchaseOrderId: string;
  supplierPartyId: string;
  state: "Draft" | "Approved" | "Executed" | "Cancelled" | string;
  resolution: string;
  reason: string;
  version: number;
  creditNoteId?: string | null;
  inventoryOperations?: string[];
}

export interface PurchaseOrderLineView {
  id: string;
  sequence: number;
  productVariantId?: string | null;
  unitOfMeasureId?: string | null;
  unitCode?: string | null;
  description: string;
  orderedQuantity: number;
  unitPrice: number;
  invoicedQuantity: number;
  cancelledQuantity: number;
  priceEvidence?: string | null;
  receivedQuantity: number;
  closedShortQuantity: number;
}

export interface PurchaseOrderView {
  id: string;
  legalEntityId: string;
  branchId?: string | null;
  supplierPartyId: string;
  currencyCode: string;
  orderDate: string;
  state: "Draft" | "PendingApproval" | "Approved" | "Sent" | "Open" | "PartiallyCompleted" | "Completed" | "Cancelled" | "Closed" | string;
  approvalState: "NotRequired" | "Pending" | "Approved" | "Rejected" | string;
  receiptState: "NotReceived" | "PartiallyReceived" | "Received" | "Closed" | string;
  invoicingState: "NotInvoiced" | "PartiallyInvoiced" | "Invoiced" | string;
  matchingState?: "NotRequired" | "Matched" | "WithinTolerance" | "Variance" | "Blocked" | "ApprovedException" | string;
  version: number;
  netTotal: number;
  lines: PurchaseOrderLineView[];
  createdAt: string;
  updatedAt: string;
}

export interface PurchaseOrderListItem {
  id: string;
  supplierPartyId: string;
  orderDate: string;
  state: string;
  currencyCode: string;
  netTotal: number;
  version: number;
  updatedAt: string;
  receiptState?: string;
  invoicingState?: string;
  approvalState?: string;
  matchingState?: string;
  poNumber?: string;
}

export interface PurchaseOrderPage {
  items: PurchaseOrderListItem[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  totalCount?: number;
}

export interface PurchaseInvoiceLinkView {
  invoiceId: string;
  purpose: string;
  idempotencyKey: string;
  createdAt: string;
}

export interface ApproveAndBillRequest {
  approval: PurchaseOrderCommandRequest;
  invoice: CreateSupplierInvoiceRequest;
}

export interface ReceiveAndBillRequest {
  receipt: ReceivePurchaseOrderRequest;
  invoice: CreateSupplierInvoiceRequest;
}

export interface LandedCostContextView {
  purchaseOrderId: string;
  legalEntityId: string;
  supplierPartyId: string;
  currencyCode: string;
  lines: {
    lineId: string;
    productVariantId?: string | null;
    receivedQuantity: number;
    unitPrice: number;
    currencyCode: string;
  }[];
}

export interface PurchasingTimelineEvent {
  id: string;
  eventType: string;
  fromState?: string | null;
  toState: string;
  reason?: string | null;
  actorName?: string | null;
  occurredAt: string;
  documentRef?: string | null;
}
