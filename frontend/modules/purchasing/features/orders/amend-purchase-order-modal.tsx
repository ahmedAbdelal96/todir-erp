import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useAmendPurchaseOrderMutation } from "../../hooks/use-purchasing-queries.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import type { PurchaseOrderView } from "../../types.ts";

export interface AmendPurchaseOrderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: PurchaseOrderView;
  readonly onAmended?: () => void;
}

export function AmendPurchaseOrderModal({
  isOpen,
  onClose,
  order,
  onAmended
}: AmendPurchaseOrderModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const amendMutation = useAmendPurchaseOrderMutation(order.legalEntityId, order.id);

  const handleAmend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setErrorMessage(t("errors.amendmentReasonRequired"));
      return;
    }

    try {
      await amendMutation.mutateAsync({
        version: order.version,
        supplierPartyId: order.supplierPartyId,
        currencyCode: order.currencyCode,
        orderDate: order.orderDate,
        lines: order.lines.map((l) => ({
          productVariantId: l.productVariantId ?? null,
          description: l.description,
          quantity: l.orderedQuantity,
          unitCode: l.unitCode ?? null,
          unitPrice: l.unitPrice,
          priceOverrideReason: reason
        }))
      });
      onClose();
      if (onAmended) onAmended();
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }} title={t("orders.amendTitle")}>
      <form onSubmit={handleAmend} style={{ display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "30rem" }}>
        {errorMessage && (
          <div role="alert" style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("orders.amendDescription")}
        </p>
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("orders.amendmentReason")} *
          </label>
          <textarea
            required
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t("orders.amendmentReasonPlaceholder")}
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
            {t("orders.confirmAmendment")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
