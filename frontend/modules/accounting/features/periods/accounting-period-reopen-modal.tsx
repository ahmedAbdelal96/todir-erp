import React, { useState } from "react";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useReopenPeriodMutation } from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { PeriodDto } from "../../types.ts";

export interface AccountingPeriodReopenModalProps {
  readonly period: PeriodDto;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const AccountingPeriodReopenModal: React.FC<AccountingPeriodReopenModalProps> = ({
  period,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [reopenReason, setReopenReason] = useState("");

  const reopenMutation = useReopenPeriodMutation();

  if (!isOpen || !period) return null;

  const handleConfirmReopen = async () => {
    if (!reopenReason.trim()) return;
    try {
      await reopenMutation.mutateAsync({
        periodId: period.id,
        request: {
          reason: reopenReason.trim(),
          idempotencyKey: `reopen-${period.id}-${Date.now()}`
        }
      });
      onSuccess?.();
      onClose();
    } catch {
      // Handled by reopenMutation error
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={`${t("reopen.modalTitle")}: ${period.code}`}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("reopen.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={reopenMutation.isPending}
            isDisabled={reopenMutation.isPending || !reopenReason.trim()}
            onPress={handleConfirmReopen}
            data-testid="btn-confirm-period-reopen"
          >
            {reopenMutation.isPending ? t("reopen.reopening") : t("reopen.confirmReopen")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="period-reopen-modal">
        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {period.code} ({period.startDate} → {period.endDate})
        </div>

        <div
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "rgba(245, 158, 11, 0.1)",
            border: "1px solid rgba(245, 158, 11, 0.3)",
            color: "var(--color-status-warning, #d97706)",
            fontSize: "0.8125rem",
            fontWeight: 600
          }}
        >
          <span aria-hidden="true">{String.fromCodePoint(0x26a0, 0xfe0f)}</span> {t("reopen.warningMessage")}
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>
          <label htmlFor="period-reopen-reason" style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("reopen.requiredReasonLabel")} *
          </label>
          <textarea
            id="period-reopen-reason"
            required
            rows={3}
            value={reopenReason}
            onChange={e => setReopenReason(e.target.value)}
            placeholder={t("reopen.reasonPlaceholder")}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)",
              color: "var(--color-content-primary)",
              fontSize: "0.875rem",
              boxSizing: "border-box",
              resize: "vertical"
            }}
          />
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>
            {t("reopen.reasonHelp")}
          </p>
        </div>

        {reopenMutation.isError && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "rgba(239, 68, 68, 0.1)",
              border: "1px solid rgba(239, 68, 68, 0.3)",
              color: "var(--color-status-danger, #dc2626)",
              fontSize: "0.875rem"
            }}
          >
            {t("reopen.errorReopening")}
          </div>
        )}
      </div>
    </Dialog>
  );
};
