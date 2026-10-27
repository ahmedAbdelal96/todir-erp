import type { DecimalString, CurrencyCode } from "../../components/contracts/domain-values.ts";

export type InvoiceDirection = "Receivable" | "Payable";
export type InvoiceDocumentType = "Invoice" | "CreditNote" | "DebitNote";
export type InvoiceLifecycle = "Draft" | "Finalized" | "Cancelled";
export type InvoicePriceMode = "Exclusive" | "Inclusive";

export interface InvoiceLineInput {
  readonly productVariantId?: string | null | undefined;
  readonly unitOfMeasureId?: string | null | undefined;
  readonly unitCode?: string | null | undefined;
  readonly description: string;
  readonly quantity: DecimalString;
  readonly unitPrice?: DecimalString | null | undefined;
  readonly discountAmount?: DecimalString | null | undefined;
  readonly sourceLineReference?: string | null | undefined;
  readonly priceOverrideReason?: string | null | undefined;
  readonly businessCategoryCode?: string | null | undefined;
}

export interface InvoiceChargeInput {
  readonly code: string;
  readonly description: string;
  readonly amount: DecimalString;
  readonly taxTreatment: string;
  readonly businessCategoryCode?: string | null | undefined;
}

export interface CreateInvoiceDraftRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly partyId: string;
  readonly addressId?: string | null | undefined;
  readonly documentDate: string; // YYYY-MM-DD
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly currencyCode: CurrencyCode;
  readonly paymentTermId?: string | null | undefined;
  readonly supplierInvoiceNumber?: string | null | undefined;
  readonly lines: readonly InvoiceLineInput[];
  readonly documentDiscountAmount?: DecimalString | null | undefined;
  readonly charges?: readonly InvoiceChargeInput[] | null | undefined;
  readonly priceMode?: InvoicePriceMode | undefined;
}

export interface UpdateInvoiceDraftRequest {
  readonly version: number;
  readonly partyId: string;
  readonly addressId?: string | null | undefined;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly currencyCode: CurrencyCode;
  readonly paymentTermId?: string | null | undefined;
  readonly supplierInvoiceNumber?: string | null | undefined;
  readonly lines: readonly InvoiceLineInput[];
  readonly documentDiscountAmount?: DecimalString | null | undefined;
  readonly charges?: readonly InvoiceChargeInput[] | null | undefined;
  readonly priceMode?: InvoicePriceMode | undefined;
}

export interface InvoiceListQuery {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly partyId?: string | null | undefined;
  readonly fromDate?: string | null | undefined;
  readonly toDate?: string | null | undefined;
  readonly currencyCode?: string | null | undefined;
  readonly lifecycle?: string | null | undefined;
  readonly search?: string | null | undefined;
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
}

export interface InvoiceListItem {
  readonly id: string;
  readonly direction: InvoiceDirection;
  readonly documentType: InvoiceDocumentType;
  readonly documentNumber?: string | null | undefined;
  readonly partyName: string;
  readonly documentDate: string;
  readonly currencyCode: CurrencyCode;
  readonly total?: DecimalString | null | undefined;
  readonly lifecycle: InvoiceLifecycle;
  readonly version: number;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly settlementSummary?: string | null | undefined;
  readonly regulatorySummary?: string | null | undefined;
  readonly dueDate?: string | null | undefined;
}

export interface InvoicePage {
  readonly items: readonly InvoiceListItem[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface InvoiceLineTaxComponent {
  readonly taxCode: string;
  readonly taxTypeCode: string;
  readonly rate: DecimalString;
  readonly taxableAmount: DecimalString;
  readonly taxAmount: DecimalString;
  readonly treatment: string;
}

export interface InvoiceLineResult {
  readonly id: string;
  readonly sequence: number;
  readonly productVariantId?: string | null | undefined;
  readonly unitOfMeasureId?: string | null | undefined;
  readonly unitCode?: string | null | undefined;
  readonly description: string;
  readonly quantity: DecimalString;
  readonly resolvedUnitPrice?: DecimalString | null | undefined;
  readonly finalUnitPrice: DecimalString;
  readonly netAmount: DecimalString;
  readonly taxAmount: DecimalString;
  readonly grossAmount: DecimalString;
  readonly priceExplanation?: string | null | undefined;
  readonly taxComponents?: readonly InvoiceLineTaxComponent[] | null | undefined;
  readonly productEvidenceJson?: string | null | undefined;
  readonly unitEvidenceJson?: string | null | undefined;
}

export interface InvoiceTotals {
  readonly netAmount: DecimalString;
  readonly discountAmount: DecimalString;
  readonly chargeAmount: DecimalString;
  readonly taxableAmount: DecimalString;
  readonly taxAmount: DecimalString;
  readonly grossAmount: DecimalString;
  readonly roundingAdjustment: DecimalString;
  readonly currencyCode: CurrencyCode;
}

export interface InvoiceScheduleLine {
  readonly sequence: number;
  readonly dueDate: string;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
}

export interface InvoiceChargeAllocationDetail {
  readonly lineId: string;
  readonly amount: DecimalString;
}

export interface InvoiceChargeDetail {
  readonly id: string;
  readonly code: string;
  readonly description: string;
  readonly amount: DecimalString;
  readonly taxTreatment: string;
  readonly allocations: readonly InvoiceChargeAllocationDetail[];
}

export interface InvoiceDiscountAllocationDetail {
  readonly lineId: string;
  readonly discountType: string;
  readonly sourceAmount: DecimalString;
  readonly allocatedAmount: DecimalString;
}

export interface InvoiceIssue {
  readonly code: string;
  readonly message: string;
  readonly severity: "Blocker" | "Warning" | "Error" | string;
  readonly lineReference?: string | null | undefined;
}

export interface InvoiceAccountingPreviewItem {
  readonly accountId: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly debit: DecimalString;
  readonly credit: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly description?: string | undefined;
}

export interface InvoiceAccountingPreview {
  readonly lines?: readonly InvoiceAccountingPreviewItem[] | undefined;
  readonly isBalanced?: boolean | undefined;
  readonly totalDebit?: DecimalString | undefined;
  readonly totalCredit?: DecimalString | undefined;
}

export interface InvoicePreview {
  readonly invoiceId: string;
  readonly version: number;
  readonly lines: readonly InvoiceLineResult[];
  readonly totals?: InvoiceTotals | null | undefined;
  readonly paymentSchedule: readonly InvoiceScheduleLine[];
  readonly warnings: readonly InvoiceIssue[];
  readonly blockers: readonly InvoiceIssue[];
  readonly readyToFinalize: boolean;
  readonly accountingPreview?: InvoiceAccountingPreview | null | undefined;
}

export interface InvoiceFinalizationRequest {
  readonly version: number;
  readonly idempotencyKey: string;
}

export interface InvoiceFinalizedResult {
  readonly invoiceId: string;
  readonly documentNumber: string;
  readonly lifecycle: InvoiceLifecycle;
  readonly totals: InvoiceTotals;
  readonly paymentSchedule: readonly InvoiceScheduleLine[];
  readonly journalEntryId: string;
  readonly openItemIds: readonly string[];
  readonly regulatoryNextAction: string;
}

export interface InvoiceSourceTrace {
  readonly module: string;
  readonly documentId: string;
  readonly purpose: string;
}

export interface InvoiceRelatedDocument {
  readonly id: string;
  readonly documentNumber: string;
  readonly documentType: InvoiceDocumentType;
  readonly lifecycle: InvoiceLifecycle;
  readonly amount: DecimalString;
  readonly date: string;
  readonly relationship?: string | undefined;
}

export interface InvoiceDetail {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly direction: InvoiceDirection;
  readonly documentType: InvoiceDocumentType;
  readonly documentNumber?: string | null | undefined;
  readonly partyId: string;
  readonly partyDisplayName: string;
  readonly partyLegalName?: string | null | undefined;
  readonly partyTradeName?: string | null | undefined;
  readonly currencyCode: CurrencyCode;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly paymentTermId?: string | null | undefined;
  readonly supplierInvoiceNumber?: string | null | undefined;
  readonly lifecycle: InvoiceLifecycle;
  readonly version: number;
  readonly totals?: InvoiceTotals | null | undefined;
  readonly lines: readonly InvoiceLineResult[];
  readonly paymentSchedule: readonly InvoiceScheduleLine[];
  readonly settlementSummary: string;
  readonly regulatorySummary: string;
  readonly journalEntryId?: string | null | undefined;
  readonly documentDiscountAmount: DecimalString;
  readonly charges?: readonly InvoiceChargeDetail[] | null | undefined;
  readonly discountAllocations?: readonly InvoiceDiscountAllocationDetail[] | null | undefined;
  readonly priceMode: InvoicePriceMode;
  readonly addressEvidenceJson?: string | null | undefined;
  readonly source?: InvoiceSourceTrace | null | undefined;
  readonly relatedDocuments?: readonly InvoiceRelatedDocument[] | undefined;
}

export interface InvoiceRenderModel {
  readonly id: string;
  readonly documentNumber: string;
  readonly direction: InvoiceDirection;
  readonly documentType: InvoiceDocumentType;
  readonly partyDisplayName: string;
  readonly partyLegalName?: string | null | undefined;
  readonly partyTradeName?: string | null | undefined;
  readonly addressEvidenceJson?: string | null | undefined;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly currencyCode: CurrencyCode;
  readonly priceMode: InvoicePriceMode;
  readonly totals: InvoiceTotals;
  readonly lines: readonly InvoiceLineResult[];
  readonly paymentSchedule: readonly InvoiceScheduleLine[];
  readonly documentDiscountAmount: DecimalString;
  readonly charges: readonly InvoiceChargeDetail[];
}

export interface InvoiceDraftResponse {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly direction: InvoiceDirection;
  readonly partyId: string;
  readonly addressId?: string | null | undefined;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly currencyCode: CurrencyCode;
  readonly paymentTermId?: string | null | undefined;
  readonly supplierInvoiceNumber?: string | null | undefined;
  readonly lifecycle: InvoiceLifecycle;
  readonly version: number;
  readonly lines: readonly InvoiceLineInput[];
  readonly documentDiscountAmount: DecimalString;
  readonly charges: readonly InvoiceChargeInput[];
  readonly priceMode: InvoicePriceMode;
  readonly source?: InvoiceSourceTrace | null | undefined;
}

export interface InvoiceCorrectionLineInput {
  readonly originalLineId: string;
  readonly quantity: DecimalString;
}

export interface InvoiceCorrectionRequest {
  readonly documentType: "CreditNote" | "DebitNote";
  readonly reason: string;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly taxDate: string;
  readonly supplyDate?: string | null | undefined;
  readonly idempotencyKey: string;
  readonly lines: readonly InvoiceCorrectionLineInput[];
  readonly documentDiscountAmount?: DecimalString | null | undefined;
  readonly charges?: readonly InvoiceChargeInput[] | null | undefined;
}

export interface CancelInvoiceDraftRequest {
  readonly version: number;
  readonly reason: string;
}

export interface InvoiceComplianceSnapshotContract {
  readonly invoiceId: string;
  readonly legalEntityId: string;
  readonly documentNumber: string;
  readonly complianceState: string;
  readonly uuid?: string | null | undefined;
  readonly hash?: string | null | undefined;
  readonly qrCodePayload?: string | null | undefined;
  readonly clearanceTimestamp?: string | null | undefined;
  readonly responsePayload?: string | null | undefined;
}

// Backwards compatibility interface for legacy visual regression & stage3 tests
export interface LegacyInvoiceLine {
  readonly id: string;
  readonly description: string;
  readonly quantity: DecimalString;
  readonly uom: string;
  readonly unitPrice: DecimalString;
  readonly discountRate?: DecimalString | undefined;
  readonly taxRate: DecimalString;
  readonly lineSubtotal: DecimalString;
  readonly lineTotal: DecimalString;
}

export interface LegacyInvoice {
  readonly id: string;
  readonly invoiceNumber: string;
  readonly issueDate: string;
  readonly dueDate: string;
  readonly customerPartyId: string;
  readonly customerName: string;
  readonly currency: CurrencyCode;
  readonly items: readonly LegacyInvoiceLine[];
  readonly subtotal: DecimalString;
  readonly totalDiscount: DecimalString;
  readonly totalTax: DecimalString;
  readonly totalAmount: DecimalString;
  readonly lifecycleStatus: "Draft" | "Issued" | "Cancelled";
  readonly settlementStatus: "Unpaid" | "PartiallyPaid" | "Paid" | "Overpaid";
  readonly approvalStatus: "Pending" | "Approved" | "Rejected";
  readonly complianceStatus: "Compliant" | "NonCompliant" | "NotApplicable";
  readonly accountingStatus: "Unposted" | "Posted";
  readonly deliveryStatus?: "Draft" | "Sent" | "Delivered" | "Failed" | undefined;
  readonly isLocked: boolean;
  readonly lockedReason?: string | undefined;
  readonly etag: string;
  readonly createdAt: string;
  readonly createdBy: string;
  readonly notes?: string | undefined;
}
