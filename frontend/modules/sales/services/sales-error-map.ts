import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedSalesError {
  readonly code: string;
  readonly messageKey: string;
  readonly status?: number;
}

const ERROR_KEY_MAP: Record<string, string> = {
  "sales.entity_mismatch": "errors.entityMismatch",
  "sales.entity_required": "errors.entityRequired",
  "sales.customer_not_found": "errors.customerNotFound",
  "sales.product_not_found": "errors.productNotFound",
  "sales.price_list_not_found": "errors.priceListNotFound",
  "sales.quotation_not_found": "errors.quotationNotFound",
  "sales.stale_quotation": "errors.staleQuotation",
  "sales.quotation_state_conflict": "errors.quotationStateConflict",
  "sales.revision_not_found": "errors.revisionNotFound",
  "sales.accepted_revision_required": "errors.acceptedRevisionRequired",
  "sales.idempotency_key_invalid": "errors.idempotencyKeyInvalid",
  "sales.idempotency_conflict": "errors.idempotencyConflict",
  "sales.lines_invalid": "errors.linesInvalid",
  "sales.line_values_invalid": "errors.lineValuesInvalid",
  "sales.order_not_found": "errors.orderNotFound",
  "sales.stale_order": "errors.staleOrder",
  "sales.order_state_conflict": "errors.orderStateConflict",
  "sales.order_nothing_to_invoice": "errors.orderNothingToInvoice",
  "sales.order_line_not_found": "errors.orderLineNotFound",
  "sales.reservation_failed": "errors.reservationFailed",
  "sales.reservation_required": "errors.reservationRequired",
  "sales.fulfillment_quantity_invalid": "errors.fulfillmentQuantityInvalid",
  "sales.fulfillment_failed": "errors.fulfillmentFailed",
  "sales.warehouse_required": "errors.warehouseRequired",
  "sales.deliver_now_requires_inventory_capability": "errors.deliverNowRequiresInventory",
  "sales.deliver_now_failed": "errors.deliverNowFailed",
  "sales.credit_blocked": "errors.creditBlocked",
  "sales.credit_approval_required": "errors.creditApprovalRequired",
  "sales.credit_note_source_required": "errors.creditNoteSourceRequired",
  "sales.credit_note_capability_unavailable": "errors.creditNoteUnavailable",
  "sales.return_lines_required": "errors.returnLinesRequired",
  "sales.return_reason_required": "errors.returnReasonRequired",
  "sales.return_quantity_invalid": "errors.returnQuantityInvalid",
  "sales.return_duplicate_source_line": "errors.returnDuplicateSourceLine",
  "sales.return_source_invalid": "errors.returnSourceInvalid",
  "sales.return_source_required": "errors.returnSourceRequired",
  "sales.return_inventory_context_required": "errors.returnInventoryContextRequired",
  "sales.return_cross_module_failed": "errors.returnCrossModuleFailed",
  "sales.inventory_capability_unavailable": "errors.inventoryCapabilityUnavailable",
  "sales.unit_of_measure_required": "errors.unitOfMeasureRequired"
};

export function mapSalesError(error: unknown): MappedSalesError {
  if (error instanceof ApiError) {
    const rawCode = (error.problemDetails as { code?: string })?.code ?? "";
    if (rawCode && ERROR_KEY_MAP[rawCode]) {
      return {
        code: rawCode,
        messageKey: ERROR_KEY_MAP[rawCode]!,
        status: error.status
      };
    }
    if (error.status === 403) {
      return {
        code: "sales.forbidden",
        messageKey: "errors.forbidden",
        status: 403
      };
    }
    if (error.status === 404) {
      return {
        code: "sales.not_found",
        messageKey: "errors.notFound",
        status: 404
      };
    }
    return {
      code: rawCode || `sales.http_${error.status}`,
      messageKey: "errors.generic",
      status: error.status
    };
  }

  return {
    code: "sales.unknown",
    messageKey: "errors.generic"
  };
}
