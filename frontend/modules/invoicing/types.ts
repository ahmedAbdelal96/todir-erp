import type { DecimalString, CurrencyCode } from "../../components/contracts/domain-values.ts";

export type InvoiceLifecycleStatus = "Draft" | "Issued" | "Cancelled";
export type InvoiceSettlementStatus = "Unpaid" | "PartiallyPaid" | "Paid" | "Overpaid";
export type InvoiceApprovalStatus = "Pending" | "Approved" | "Rejected";
export type InvoiceComplianceStatus = "Compliant" | "NonCompliant" | "NotApplicable";
export type InvoiceAccountingStatus = "Unposted" | "Posted";
export type InvoiceDeliveryStatus = "Draft" | "Sent" | "Delivered" | "Failed";

export interface InvoiceLine {
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

export interface Invoice {
  readonly id: string;
  readonly invoiceNumber: string;
  readonly issueDate: string;
  readonly dueDate: string;
  readonly customerPartyId: string;
  readonly customerName: string;
  readonly currency: CurrencyCode;
  readonly items: readonly InvoiceLine[];
  readonly subtotal: DecimalString;
  readonly totalDiscount: DecimalString;
  readonly totalTax: DecimalString;
  readonly totalAmount: DecimalString;
  readonly lifecycleStatus: InvoiceLifecycleStatus;
  readonly settlementStatus: InvoiceSettlementStatus;
  readonly approvalStatus: InvoiceApprovalStatus;
  readonly complianceStatus: InvoiceComplianceStatus;
  readonly accountingStatus: InvoiceAccountingStatus;
  readonly deliveryStatus?: InvoiceDeliveryStatus | undefined;
  readonly isLocked: boolean;
  readonly lockedReason?: string | undefined;
  readonly etag: string;
  readonly createdAt: string;
  readonly createdBy: string;
  readonly notes?: string | undefined;
}

export interface InvoicePreviewLineInput {
  readonly description: string;
  readonly quantity: DecimalString;
  readonly uom: string;
  readonly unitPrice: DecimalString;
  readonly discountRate?: DecimalString | undefined;
  readonly taxRate: DecimalString;
}

export interface InvoicePreviewRequest {
  readonly currency: CurrencyCode;
  readonly items: readonly InvoicePreviewLineInput[];
}

export interface InvoicePreviewResponse {
  readonly subtotal: DecimalString;
  readonly totalDiscount: DecimalString;
  readonly totalTax: DecimalString;
  readonly totalAmount: DecimalString;
  readonly lineCalculations: readonly {
    readonly lineSubtotal: DecimalString;
    readonly lineTotal: DecimalString;
  }[];
}
