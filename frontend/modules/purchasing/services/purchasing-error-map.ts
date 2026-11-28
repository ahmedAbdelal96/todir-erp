import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedPurchasingError {
  readonly code: string;
  readonly messageKey: string;
  readonly status?: number;
}

const ERROR_MAP: Record<string, string> = {
  "purchasing.supplier_not_ready": "errors.supplierNotReady",
  "purchasing.supplier_blocked": "errors.supplierBlocked",
  "purchasing.po_not_found": "errors.poNotFound",
  "purchasing.po_stale": "errors.poStale",
  "purchasing.po_not_approvable": "errors.poNotApprovable",
  "purchasing.po_not_approved": "errors.poNotApproved",
  "purchasing.po_already_closed": "errors.poAlreadyClosed",
  "purchasing.po_conversion_conflict": "errors.poConversionConflict",
  "purchasing.price_override_not_allowed": "errors.priceOverrideNotAllowed",
  "purchasing.approval_required": "errors.approvalRequired",
  "purchasing.inventory_not_available": "errors.inventoryNotAvailable",
  "purchasing.receipt_not_allowed": "errors.receiptNotAllowed",
  "purchasing.over_receipt_not_allowed": "errors.overReceiptNotAllowed",
  "purchasing.over_receipt_approval_required": "errors.overReceiptApprovalRequired",
  "purchasing.receipt_quantity_conflict": "errors.receiptQuantityConflict",
  "purchasing.invoice_quantity_exceeded": "errors.invoiceQuantityExceeded",
  "purchasing.invoice_source_conflict": "errors.invoiceSourceConflict",
  "purchasing.supplier_invoice_duplicate": "errors.supplierInvoiceDuplicate",
  "purchasing.match_variance": "errors.matchVariance",
  "purchasing.match_blocked": "errors.matchBlocked",
  "purchasing.match_stale": "errors.matchStale",
  "purchasing.return_quantity_exceeded": "errors.returnQuantityExceeded",
  "purchasing.return_not_allowed": "errors.returnNotAllowed",
  "purchasing.idempotency_conflict": "errors.idempotencyConflict",
  "purchasing.permission_denied": "errors.permissionDenied",
  "purchasing.lines_invalid": "errors.linesInvalid",
  "purchasing.buy_price_missing": "errors.buyPriceMissing",
  "purchasing.buy_price_required": "errors.buyPriceRequired",
  "purchasing.close_short_reason_required": "errors.closeShortReasonRequired",
  "purchasing.approved_requires_amendment": "errors.approvedRequiresAmendment",
  "planning.budget_approval_required": "errors.budgetApprovalRequired",
  "planning.budget_exceeded": "errors.budgetExceeded",
  "planning.budget_bucket_not_found": "errors.budgetBucketInvalid",
  "planning.budget_bucket_required": "errors.budgetBucketInvalid"
};

export function mapPurchasingError(error: unknown): MappedPurchasingError {
  if (error instanceof ApiError) {
    const rawCode = (error.problemDetails as { code?: string } | undefined)?.code ?? "";
    if (rawCode && ERROR_MAP[rawCode]) {
      return { code: rawCode, messageKey: ERROR_MAP[rawCode]!, status: error.status };
    }
    if (error.status === 403) {
      return { code: "purchasing.forbidden", messageKey: "errors.forbidden", status: 403 };
    }
    if (error.status === 404) {
      return { code: "purchasing.not_found", messageKey: "errors.poNotFound", status: 404 };
    }
    if (error.status === 409) {
      return { code: "purchasing.conflict", messageKey: "errors.poStale", status: 409 };
    }
    return {
      code: rawCode || `purchasing.http_${error.status}`,
      messageKey: "errors.generic",
      status: error.status
    };
  }

  if (error instanceof Error && error.message) {
    if (ERROR_MAP[error.message]) {
      return { code: error.message, messageKey: ERROR_MAP[error.message]! };
    }
  }

  return { code: "purchasing.unknown", messageKey: "errors.generic" };
}
