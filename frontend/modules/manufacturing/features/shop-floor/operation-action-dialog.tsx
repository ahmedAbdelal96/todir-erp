import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useTransitionOperationMutation } from "../../hooks/use-manufacturing.ts";
import type { ProductionOperation } from "../../types.ts";

export interface OperationActionDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly operationId: string;
  readonly operationName?: string;
  readonly operation?: ProductionOperation;
  readonly action: "start" | "pause" | "resume" | "complete" | "skip";
  readonly currentVersion?: number;
}

export function OperationActionDialog({
  isOpen,
  onClose,
  orderId,
  operationId,
  operationName,
  operation,
  action,
  currentVersion
}: OperationActionDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const transitionMutation = useTransitionOperationMutation();

  const [quantity, setQuantity] = useState("10");
  const [actualMinutes, setActualMinutes] = useState("30");
  const [reason, setReason] = useState("");
  const [idempotencyKey] = useState(() => `op-act-${Date.now()}`);

  const getActionTitle = () => {
    switch (action) {
      case "start":
        return t("shopFloor.startOperation");
      case "pause":
        return t("shopFloor.pauseOperation");
      case "resume":
        return t("shopFloor.resumeOperation");
      case "complete":
        return t("shopFloor.completeOperation");
      case "skip":
        return t("status.Skipped");
    }
  };

  const nameDisplay = operationName ?? operation?.name ?? "";
  const effectiveVersion = currentVersion ?? operation?.version ?? 0;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    transitionMutation.mutate(
      {
        operationId,
        action,
        request: {
          expectedVersion: effectiveVersion,
          idempotencyKey,
          reason: reason || undefined,
          quantity: action === "complete" ? parseFloat(quantity) || undefined : undefined,
          actualMinutes: parseFloat(actualMinutes) || undefined
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={`${getActionTitle()} — ${nameDisplay}`}
      description={t("shopFloor.subtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {transitionMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.concurrencyConflict")}
          </div>
        ) : null}

        {action === "complete" ? (
          <TextField
            label={t("shopFloor.goodOutput")}
            type="number"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            min="0.0001"
            step="any"
            required
          />
        ) : null}

        <TextField
          label={t("shopFloor.actualMinutes")}
          type="number"
          value={actualMinutes}
          onChange={(e) => setActualMinutes(e.target.value)}
          min="0"
          step="any"
        />

        {action === "pause" || action === "skip" ? (
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
          <Button type="submit" variant="primary" isDisabled={transitionMutation.isPending}>
            {transitionMutation.isPending ? t("common.loading") : getActionTitle()}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
