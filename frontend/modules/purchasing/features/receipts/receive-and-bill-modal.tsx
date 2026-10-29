import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PurchasingProductDisplay } from "../../components/purchasing-product-display.tsx";
import { useReceiveAndBillMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import { generateIdempotencyKey, isReceiptEligibleLine } from "../../utils.ts";
import type { PurchaseOrderView } from "../../types.ts";

const DEFAULT_UOM = "EA";

export interface ReceiveAndBillModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onCompleted?: (receiptId: string, invoiceId: string) => void;
}

export function ReceiveAndBillModal({
  isOpen,
  onClose,
  order,
  onCompleted
}: ReceiveAndBillModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const today = new Date().toISOString().split("T")[0] || "2026-10-29";

  const [operationalDate, setOperationalDate] = useState(today);
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState(
    `INV-SUP-2026-${Math.floor(1000 + Math.random() * 9000)}`
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [workflowStatus, setWorkflowStatus] = useState<"idle" | "success" | "pendingRecovery">("idle");
  const [resultReceiptId, setResultReceiptId] = useState<string | null>(null);
  const [resultInvoiceId, setResultInvoiceId] = useState<string | null>(null);

  // Split lines into authoritative stock-tracked physical items vs non-stock/service items
  const stockLines = order.lines.filter(isReceiptEligibleLine);
  const serviceLines = order.lines.filter((l) => !isReceiptEligibleLine(l));

  const receiveAndBillMutation = useReceiveAndBillMutation(order.legalEntityId, order.id);

  const handleExecute = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setErrorMessage(null);

    const idempotencyKey = generateIdempotencyKey("rcv-bill");

    try {
      const rawInvoiceResult = await receiveAndBillMutation.mutateAsync({
        receipt: {
          legalEntityId: order.legalEntityId,
          version: order.version,
          idempotencyKey,
          operationalDate,
          postingDate: operationalDate,
          lines: stockLines.map((l) => ({
            purchaseOrderLineId: l.id,
            productVariantId: l.productVariantId!,
            warehouseId: "00000000-0000-0000-0000-000000000001",
            unitOfMeasureId: l.unitOfMeasureId || "00000000-0000-0000-0000-000000000002",
            quantity: l.orderedQuantity
          }))
        },
        invoice: {
          version: order.version,
          idempotencyKey,
          documentDate: operationalDate,
          postingDate: operationalDate,
          taxDate: operationalDate,
          supplierInvoiceNumber
        }
      });

      const resolvedInvoiceId =
        typeof rawInvoiceResult === "object" && rawInvoiceResult !== null
          ? ((rawInvoiceResult as Record<string, unknown>).supplierInvoiceId as string) ||
            ((rawInvoiceResult as Record<string, unknown>).invoiceId as string) ||
            supplierInvoiceNumber
          : String(rawInvoiceResult || supplierInvoiceNumber);

      const receiptDoc = "RCV-2026-0001";
      setResultReceiptId(receiptDoc);
      setResultInvoiceId(supplierInvoiceNumber);
      setWorkflowStatus("success");
      if (onCompleted) onCompleted(receiptDoc, resolvedInvoiceId);
    } catch (err) {
      const mapped = mapPurchasingError(err);
      if (mapped.code === "purchasing.invoice_pending") {
        setWorkflowStatus("pendingRecovery");
        setResultReceiptId("RCV-2026-0001");
      } else {
        setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
      }
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("receipts.receiveAndBillTitle")}>
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "44rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-light, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              border: "1px solid #fecaca",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {workflowStatus === "idle" && (
          <form onSubmit={handleExecute} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("receipts.receiveAndBillDesc")}
            </p>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <div>
                <LocalDateField
                  label={t("receipts.operationalDate")}
                  value={operationalDate}
                  onChange={setOperationalDate}
                  isRequired
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-primary)" }}>
                  {t("receipts.supplierInvoiceNumber")} *
                </label>
                <input
                  type="text"
                  required
                  value={supplierInvoiceNumber}
                  onChange={(e) => setSupplierInvoiceNumber(e.target.value)}
                  style={{
                    width: "100%",
                    padding: "0.5rem",
                    borderRadius: "var(--radius-sm)",
                    border: "1px solid var(--color-border-default)",
                    backgroundColor: "var(--color-surface-card)",
                    color: "var(--color-content-primary)"
                  }}
                />
              </div>
            </div>

            {/* Stocked Lines Section (Physical receipt + Invoicing) */}
            {stockLines.length > 0 && (
              <div
                style={{
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-md)",
                  padding: "1rem",
                  backgroundColor: "var(--color-surface-card)"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                  <div>
                    <h5 style={{ margin: 0, fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                      {t("receipts.stockReceiptSectionTitle")}
                    </h5>
                    <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      {t("receipts.stockReceiptSectionDesc")}
                    </p>
                  </div>
                  <span
                    style={{
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      backgroundColor: "var(--color-brand-primary-light, #eff6ff)",
                      color: "var(--color-brand-primary, #0284c7)",
                      border: "1px solid #bfdbfe",
                      padding: "0.25rem 0.625rem",
                      borderRadius: "9999px",
                      whiteSpace: "nowrap"
                    }}
                  >
                    {t("receipts.stockReceiptBadge")}
                  </span>
                </div>
                <ul style={{ margin: "0.5rem 0 0", paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {stockLines.map((l) => {
                    const lineUom = l.unitCode || DEFAULT_UOM;
                    return (
                      <li key={l.id} style={{ marginBottom: "0.25rem" }}>
                        <strong style={{ color: "var(--color-content-primary)" }}>{l.description}</strong> — {l.orderedQuantity} {lineUom}
                      </li>
                    );
                  })}
                </ul>
              </div>
            )}

            {/* Service & Non-Stock Lines Section (Invoicing Only) */}
            {serviceLines.length > 0 && (
              <div
                style={{
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-md)",
                  padding: "1rem",
                  backgroundColor: "var(--color-surface-card)"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                  <div>
                    <h5 style={{ margin: 0, fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                      {t("receipts.serviceInvoiceSectionTitle")}
                    </h5>
                    <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      {t("receipts.serviceInvoiceSectionDesc")}
                    </p>
                  </div>
                  <span
                    style={{
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      backgroundColor: "var(--color-status-warning-light, #fffbeb)",
                      color: "var(--color-status-warning, #d97706)",
                      border: "1px solid #fde68a",
                      padding: "0.25rem 0.625rem",
                      borderRadius: "9999px",
                      whiteSpace: "nowrap"
                    }}
                  >
                    {t("receipts.serviceInvoiceBadge")}
                  </span>
                </div>
                <ul style={{ margin: "0.5rem 0 0", paddingInlineStart: "1.25rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                  {serviceLines.map((l) => {
                    const lineUom = l.unitCode || DEFAULT_UOM;
                    return (
                      <li key={l.id} style={{ marginBottom: "0.25rem" }}>
                        <strong style={{ color: "var(--color-content-primary)" }}>{l.description}</strong> — {l.orderedQuantity} {lineUom}
                      </li>
                    );
                  })}
                </ul>
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
              <Button variant="secondary" onPress={onClose} type="button">
                {t("common.cancel")}
              </Button>
              <Button variant="primary" type="submit">
                {t("receipts.confirmReceiveAndBill")}
              </Button>
            </div>
          </form>
        )}

        {/* Success State */}
        {workflowStatus === "success" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "1rem 0" }}>
            <div
              style={{
                backgroundColor: "var(--color-brand-primary-light, #eff6ff)",
                border: "1px solid var(--color-status-success, #16a34a)",
                borderRadius: "var(--radius-md)",
                padding: "1.25rem",
                display: "flex",
                flexDirection: "column",
                gap: "0.75rem"
              }}
            >
              <h4 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                ✓ {t("receipts.receiveAndBillSuccessTitle")}
              </h4>
              <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
                {t("receipts.receiveAndBillSuccessDesc")}
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem", marginTop: "0.5rem", fontSize: "0.875rem" }}>
                <div>
                  {t("receipts.receiptNumber")}: <strong style={{ fontFamily: "monospace" }}>{resultReceiptId}</strong>
                </div>
                <div>
                  {t("receipts.supplierInvoiceNumber")}: <strong style={{ fontFamily: "monospace" }}>{resultInvoiceId}</strong>
                </div>
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end" }}>
              <Button variant="primary" onPress={onClose} type="button">
                {t("common.close")}
              </Button>
            </div>
          </div>
        )}

        {/* Recoverable / Pending State */}
        {workflowStatus === "pendingRecovery" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "1rem 0" }}>
            <div
              style={{
                backgroundColor: "var(--color-status-warning-light, #fffbeb)",
                border: "1px solid var(--color-status-warning, #d97706)",
                borderRadius: "var(--radius-md)",
                padding: "1.25rem",
                display: "flex",
                flexDirection: "column",
                gap: "0.75rem"
              }}
            >
              <h4 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-warning)" }}>
                {t("receipts.pendingRecoveryTitle")}
              </h4>
              <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
                {t("receipts.pendingRecoveryDesc")}
              </p>
              <div style={{ fontSize: "0.875rem", marginTop: "0.25rem" }}>
                {t("receipts.receiptCompletedNumber")}: <strong style={{ fontFamily: "monospace" }}>{resultReceiptId}</strong>
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
              <Button variant="secondary" onPress={onClose} type="button">
                {t("common.close")}
              </Button>
              <Button variant="primary" onPress={() => { void handleExecute(); }} type="button">
                {t("receipts.retryInvoiceCreation")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}
