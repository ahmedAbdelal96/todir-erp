import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCancelRemainingPurchaseOrderMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import { generateIdempotencyKey } from "../../utils.ts";
import type { PurchaseOrderView } from "../../types.ts";

export interface CancelRemainingModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onCancelled?: () => void;
}

export function CancelRemainingModal({
  isOpen,
  onClose,
  order,
  onCancelled
}: CancelRemainingModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const cancelMutation = useCancelRemainingPurchaseOrderMutation(order.legalEntityId, order.id);

  const handleCancel = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await cancelMutation.mutateAsync({
        version: order.version,
        idempotencyKey: generateIdempotencyKey("cancel"),
        reason: reason.trim() || null
      });
      onClose();
      if (onCancelled) onCancelled();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const totalOrdered = order.lines.reduce((sum, l) => sum + l.orderedQuantity, 0);
  const totalReceived = order.lines.reduce((sum, l) => sum + l.receivedQuantity, 0);
  const totalRemaining = Math.max(0, totalOrdered - totalReceived);

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("orders.cancelRemainingTitle")}>
      <form onSubmit={handleCancel} style={{ display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "30rem" }}>
        {errorMessage && (
          <div role="alert" style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}
        <div style={{ backgroundColor: "var(--color-surface-subtle)", padding: "0.75rem", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem" }}>
          <div>{t("orders.orderedQuantity")}: <strong>{totalOrdered}</strong></div>
          <div>{t("orders.receivedQuantity")}: <strong>{totalReceived}</strong></div>
          <div>{t("orders.cancelledRemainingQuantity")}: <strong>{totalRemaining}</strong></div>
        </div>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("orders.cancelRemainingNotice")}
        </p>
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("orders.reason")}
          </label>
          <input
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("orders.reasonPlaceholder")}
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
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit">
            {t("orders.confirmCancelRemaining")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
