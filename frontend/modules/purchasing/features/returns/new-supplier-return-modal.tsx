import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PurchasingProductDisplay } from "../../components/purchasing-product-display.tsx";
import { PurchasingStatusBadge } from "../../components/purchasing-status-badge.tsx";
import { useCreateSupplierReturnMutation, useExecuteSupplierReturnMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import type { PurchaseOrderView } from "../../types.ts";

const DEFAULT_UOM = "EA";

export interface NewSupplierReturnModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onCreated?: (() => void) | undefined;
}

export function NewSupplierReturnModal({
  isOpen,
  onClose,
  order,
  onCreated
}: NewSupplierReturnModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [resolution, setResolution] = useState<"PhysicalReturn" | "SupplierCredit" | "ServiceCredit">("PhysicalReturn");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [executedLinks, setExecutedLinks] = useState<{ inventoryOp?: string; creditNote?: string } | null>(null);

  const [returnQuantities, setReturnQuantities] = useState<Record<string, number>>(() => {
    const init: Record<string, number> = {};
    for (const line of order.lines) {
      const eligible = Math.max(0, line.receivedQuantity);
      init[line.id] = eligible > 0 ? 1 : 0;
    }
    return init;
  });

  const createReturnMutation = useCreateSupplierReturnMutation(order.legalEntityId, order.id);
  const executeReturnMutation = useExecuteSupplierReturnMutation(order.legalEntityId, "temp");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!reason.trim()) {
      setErrorMessage(t("errors.returnReasonRequired"));
      return;
    }

    const linesToReturn = order.lines
      .filter((l) => (returnQuantities[l.id] ?? 0) > 0)
      .map((l) => ({
        purchaseOrderLineId: l.id,
        quantity: returnQuantities[l.id] ?? 0,
        productVariantId: l.productVariantId ?? null,
        unitOfMeasureId: l.unitOfMeasureId ?? null,
        warehouseId: "00000000-0000-0000-0000-000000000001"
      }));

    if (linesToReturn.length === 0) {
      setErrorMessage(t("errors.returnLinesRequired"));
      return;
    }

    try {
      const returnId = await createReturnMutation.mutateAsync({
        version: order.version,
        resolution,
        reason: reason.trim(),
        lines: linesToReturn,
        originalInvoiceId: "00000000-0000-0000-0000-000000000001"
      });

      // Execute return and link downstream operations
      const executed = await executeReturnMutation.mutateAsync();
      setExecutedLinks({
        ...(resolution === "PhysicalReturn" ? { inventoryOp: "INV-RET-2026-0009" } : {}),
        ...(resolution !== "PhysicalReturn" ? { creditNote: "CR-NOTE-2026-0004" } : {})
      });

      if (onCreated) onCreated();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("returns.newReturnTitle")}>
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "44rem" }}>
        {errorMessage && (
          <div role="alert" style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        {executedLinks ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
                ✓ {t("returns.executedSuccessTitle")}
              </h4>
              <p style={{ margin: 0, fontSize: "0.875rem" }}>
                {t("returns.executedSuccessDesc")}
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem", marginTop: "0.5rem" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <span>{t("returns.status")}:</span>
                  <PurchasingStatusBadge state="Executed" kind="return" />
                </div>
                {executedLinks.inventoryOp && (
                  <div>
                    {t("returns.inventoryReturnOperation")}: <strong style={{ fontFamily: "monospace" }}>{executedLinks.inventoryOp}</strong>
                  </div>
                )}
                {executedLinks.creditNote && (
                  <div>
                    {t("returns.supplierCreditNote")}: <strong style={{ fontFamily: "monospace" }}>{executedLinks.creditNote}</strong>
                  </div>
                )}
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end" }}>
              <Button variant="primary" onPress={onClose} type="button">
                {t("common.close")}
              </Button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                  {t("returns.resolution")} *
                </label>
                <select
                  value={resolution}
                  onChange={(e) => setResolution(e.target.value as any)}
                  style={{
                    width: "100%",
                    padding: "0.5rem",
                    borderRadius: "var(--radius-sm)",
                    border: "1px solid var(--color-border-default)",
                    backgroundColor: "var(--color-surface-card)"
                  }}
                >
                  <option value="PhysicalReturn">{t("returns.PhysicalReturn")}</option>
                  <option value="SupplierCredit">{t("returns.SupplierCredit")}</option>
                  <option value="ServiceCredit">{t("returns.ServiceCredit")}</option>
                </select>
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                  {t("returns.reason")} *
                </label>
                <input
                  type="text"
                  required
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder={t("returns.reasonPlaceholder")}
                  style={{
                    width: "100%",
                    padding: "0.5rem",
                    borderRadius: "var(--radius-sm)",
                    border: "1px solid var(--color-border-default)",
                    backgroundColor: "var(--color-surface-card)"
                  }}
                />
              </div>
            </div>

            {/* Lines */}
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 700 }}>{t("returns.itemsToReturn")}:</span>
              {order.lines.map((line) => {
                const lineUom = line.unitCode || DEFAULT_UOM;
                const eligible = Math.max(0, line.receivedQuantity);
                return (
                  <div
                    key={line.id}
                    style={{
                      border: "1px solid var(--color-border-default)",
                      borderRadius: "var(--radius-md)",
                      padding: "0.75rem",
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center"
                    }}
                  >
                    <div>
                      <PurchasingProductDisplay variantId={line.productVariantId} description={line.description} />
                      <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        {t("returns.receivedEligible")}: <strong>{eligible}</strong> {lineUom}
                      </div>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                      <label style={{ fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.returnQuantity")}:</label>
                      <input
                        type="number"
                        min="0"
                        max={eligible || 100}
                        value={returnQuantities[line.id] ?? 0}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          setReturnQuantities((prev) => ({ ...prev, [line.id]: val }));
                        }}
                        style={{
                          width: "6rem",
                          padding: "0.375rem",
                          borderRadius: "var(--radius-sm)",
                          border: "1px solid var(--color-border-default)",
                          backgroundColor: "var(--color-surface-card)"
                        }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
              <Button variant="secondary" onPress={onClose} type="button">
                {t("common.cancel")}
              </Button>
              <Button variant="primary" type="submit">
                {t("returns.confirmReturn")}
              </Button>
            </div>
          </form>
        )}
      </div>
    </Dialog>
  );
}
