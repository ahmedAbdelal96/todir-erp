import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { PurchasingProductDisplay } from "../../components/purchasing-product-display.tsx";
import { useReceivePurchaseOrderMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import { generateIdempotencyKey, isReceiptEligibleLine } from "../../utils.ts";
import type { PurchaseOrderView } from "../../types.ts";

const DEFAULT_UOM = "EA";

export interface ReceiveOrderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onReceived?: (receiptIds: string[]) => void;
}

export function ReceiveOrderModal({
  isOpen,
  onClose,
  order,
  onReceived
}: ReceiveOrderModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const today = new Date().toISOString().split("T")[0] || "2026-10-29";

  const [operationalDate, setOperationalDate] = useState(today);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Authoritative physical stock-tracked lines only (services, non-stock goods, and ad-hoc lines are excluded from inventory receipt)
  const physicalLines = order.lines.filter(isReceiptEligibleLine);

  // Line receipt quantities
  const [lineQuantities, setLineQuantities] = useState<Record<string, number>>(() => {
    const init: Record<string, number> = {};
    for (const line of physicalLines) {
      const remaining = Math.max(0, line.orderedQuantity - line.receivedQuantity);
      init[line.id] = remaining > 0 ? remaining : line.orderedQuantity;
    }
    return init;
  });

  const receiveMutation = useReceivePurchaseOrderMutation(order.legalEntityId, order.id);

  const handleReceive = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (physicalLines.length === 0) {
      setErrorMessage(t("receipts.noPhysicalLines"));
      return;
    }

    // Over-receipt check on physical lines only
    for (const line of physicalLines) {
      const attempted = lineQuantities[line.id] ?? 0;
      const allowedRemaining = Math.max(0, line.orderedQuantity - line.receivedQuantity);
      if (attempted > allowedRemaining) {
        setErrorMessage(
          `${t("errors.overReceiptNotAllowed")}: ${t("orders.orderedQuantity")} (${line.orderedQuantity}) - ${t("orders.receivedQuantity")} (${line.receivedQuantity}) - ${t("orders.attempted")} (${attempted})`
        );
        return;
      }
    }

    try {
      const result = await receiveMutation.mutateAsync({
        legalEntityId: order.legalEntityId,
        version: order.version,
        idempotencyKey: generateIdempotencyKey("rcv"),
        operationalDate,
        postingDate: operationalDate,
        lines: physicalLines.map((l) => ({
          purchaseOrderLineId: l.id,
          productVariantId: l.productVariantId!,
          warehouseId: "00000000-0000-0000-0000-000000000001",
          unitOfMeasureId: l.unitOfMeasureId || "00000000-0000-0000-0000-000000000002",
          quantity: lineQuantities[l.id] ?? l.orderedQuantity
        }))
      });

      onClose();
      if (onReceived) onReceived(result.receiptIds || ["RCV-2026-0001"]);
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("receipts.receiveTitle")}>
      <form onSubmit={handleReceive} style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "44rem" }}>
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
              {t("receipts.destinationWarehouse")}
            </label>
            <div style={{ padding: "0.5rem 0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", backgroundColor: "var(--color-surface-subtle)", fontSize: "0.875rem" }}>
              {t("receipts.mainWarehouse")}
            </div>
          </div>
        </div>

        {/* Lines */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <h4 style={{ margin: 0, fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("receipts.receiptLines")}
          </h4>

          {physicalLines.length === 0 ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)", border: "1px dashed var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
              {t("receipts.noPhysicalLines")}
            </div>
          ) : (
            physicalLines.map((line) => {
              const lineUom = line.unitCode || DEFAULT_UOM;
              const remaining = Math.max(0, line.orderedQuantity - line.receivedQuantity);
              return (
                <div
                  key={line.id}
                  style={{
                    border: "1px solid var(--color-border-default)",
                    borderRadius: "var(--radius-md)",
                    padding: "0.875rem",
                    backgroundColor: "var(--color-surface-card)",
                    display: "flex",
                    flexDirection: "column",
                    gap: "0.5rem"
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                    <PurchasingProductDisplay variantId={line.productVariantId} description={line.description} />
                    <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("orders.orderedQuantity")}: <strong>{line.orderedQuantity}</strong> | {t("orders.receivedQuantity")}: <strong>{line.receivedQuantity}</strong> | {t("orders.remainingQuantity")}: <strong>{remaining}</strong>
                    </div>
                  </div>

                  <div style={{ display: "flex", alignItems: "center", gap: "1rem", marginTop: "0.25rem" }}>
                    <label style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("receipts.quantityToReceive")}:
                    </label>
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={lineQuantities[line.id] ?? remaining}
                      onChange={(e) => {
                        const val = parseFloat(e.target.value) || 0;
                        setLineQuantities((prev) => ({ ...prev, [line.id]: val }));
                      }}
                      style={{
                        width: "8rem",
                        padding: "0.375rem 0.5rem",
                        borderRadius: "var(--radius-sm)",
                        border: "1px solid var(--color-border-default)",
                        backgroundColor: "var(--color-surface-card)",
                        color: "var(--color-content-primary)"
                      }}
                    />
                    <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {lineUom}
                    </span>
                  </div>
                </div>
              );
            })
          )}
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit">
            {t("receipts.confirmReceive")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
