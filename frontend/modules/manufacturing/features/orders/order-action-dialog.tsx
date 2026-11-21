import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useTransitionProductionOrderMutation } from "../../hooks/use-manufacturing.ts";
import type { ProductionOrder } from "../../types.ts";

export interface OrderActionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly order: ProductionOrder;
  readonly action: "release" | "start" | "hold" | "resume" | "complete" | "close" | "cancel";
}

export function OrderActionDialog({ isOpen, onClose, order, action }: OrderActionDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const transitionMutation = useTransitionProductionOrderMutation();
  const [reason, setReason] = useState("");

  const requiresReason = action === "hold" || action === "cancel" || action === "close";

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (requiresReason && !reason.trim()) return;

    transitionMutation.mutate(
      {
        orderId: order.id,
        action,
        request: {
          expectedVersion: order.version,
          reason: reason.trim() || undefined
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const getActionLabel = () => {
    return t(`orders.actions.${action}`);
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={`${getActionLabel()} — ${order.orderNumber}`}
      description={t("orders.confirmActionDesc")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {transitionMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.concurrencyConflict")}
          </div>
        ) : null}

        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          <div>{t("orders.orderNumber")}: <strong>{order.orderNumber}</strong></div>
          <div>{t("orders.stateLabel")}: <strong>{t(`status.${order.state}`)}</strong></div>
        </div>

        {requiresReason ? (
          <TextField
            label={t("orders.reasonLabel")}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
          />
        ) : null}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button
            type="submit"
            variant={action === "cancel" ? "destructive" : "primary"}
            isDisabled={transitionMutation.isPending || (requiresReason && !reason.trim())}
          >
            {transitionMutation.isPending ? t("common.loading") : getActionLabel()}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
