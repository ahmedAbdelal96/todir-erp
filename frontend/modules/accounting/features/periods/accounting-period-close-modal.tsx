import React, { useEffect, useState } from "react";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import {
  usePreviewCloseMutation,
  useClosePeriodMutation
} from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { PeriodDto, CloseRunView, CloseCheckView } from "../../types.ts";
import {
  formatHealthCode,
  formatStatus,
  formatDiagnosticMessage
} from "../../utils/diagnostic-translations.ts";

export interface AccountingPeriodCloseModalProps {
  readonly period: PeriodDto;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const AccountingPeriodCloseModal: React.FC<AccountingPeriodCloseModalProps> = ({
  period,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [closeReason, setCloseReason] = useState("");
  const [previewRun, setPreviewRun] = useState<CloseRunView | null>(null);

  const previewMutation = usePreviewCloseMutation();
  const closeMutation = useClosePeriodMutation();

  useEffect(() => {
    if (isOpen && period) {
      setCloseReason("");
      previewMutation
        .mutateAsync({
          periodId: period.id,
          idempotencyKey: `close-prev-${period.id}-${Date.now()}`
        })
        .then(res => setPreviewRun(res))
        .catch(() => {});
    }
  }, [isOpen, period?.id]);

  if (!isOpen || !period) return null;

  const hasBlockingErrors = (previewRun?.checks ?? []).some(
    check => check.severity === "Blocking" && check.status !== "Pass"
  );

  const handleConfirmClose = async () => {
    if (hasBlockingErrors) return;
    try {
      await closeMutation.mutateAsync({
        periodId: period.id,
        idempotencyKey: `close-exec-${period.id}-${Date.now()}`,
        reason: closeReason.trim() || undefined
      });
      onSuccess?.();
      onClose();
    } catch {
      // Handled by closeMutation
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={`${t("close.modalTitle")}: ${period.code}`}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("close.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={closeMutation.isPending}
            isDisabled={closeMutation.isPending || previewMutation.isPending || hasBlockingErrors}
            onPress={handleConfirmClose}
            data-testid="btn-confirm-period-close"
          >
            {closeMutation.isPending ? t("close.closing") : t("close.confirmClose")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="period-close-modal">
        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {period.code} ({period.startDate} → {period.endDate})
        </div>

        {previewMutation.isPending && (
          <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("close.previewingChecks")}
          </div>
        )}

        {previewMutation.isError && (
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
            {t("close.previewError")}
          </div>
        )}

        {previewRun && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <div style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-secondary)", textTransform: "uppercase" }}>
              {t("close.checklistTitle")}
            </div>

            <div
              style={{
                maxHeight: "15rem",
                overflowY: "auto",
                border: "1px solid var(--color-border-default)",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              {previewRun.checks.map((check: CloseCheckView, idx) => (
                <div
                  key={idx}
                  style={{
                    padding: "0.75rem 1rem",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    gap: "0.75rem",
                    borderBottom: idx < previewRun.checks.length - 1 ? "1px solid var(--color-border-subtle, #f1f5f9)" : "none"
                  }}
                >
                  <div>
                    <span style={{ fontWeight: 700, color: "var(--color-content-primary)", fontSize: "0.875rem", display: "block" }}>
                      {formatHealthCode(check.code)}
                    </span>
                    <span style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                      <bdi>{check.code}</bdi>
                    </span>
                    <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-secondary)", fontSize: "0.8125rem", lineHeight: 1.4 }}>
                      {formatDiagnosticMessage(check.message)}
                    </p>
                  </div>
                  <StatusBadge
                    label={formatStatus(check.status)}
                    intent={
                      check.status === "Pass"
                        ? "success"
                        : check.severity === "Blocking"
                        ? "danger"
                        : "warning"
                    }
                  />
                </div>
              ))}
            </div>

            {hasBlockingErrors && (
              <div
                style={{
                  padding: "0.75rem 1rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "rgba(239, 68, 68, 0.1)",
                  border: "1px solid rgba(239, 68, 68, 0.3)",
                  color: "var(--color-status-danger, #dc2626)",
                  fontSize: "0.8125rem",
                  fontWeight: 600
                }}
              >
                ⚠️ {t("close.blockingErrorWarning")}
              </div>
            )}
          </div>
        )}

        <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>
          <label htmlFor="period-close-note" style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("close.optionalReasonLabel")}
          </label>
          <input
            id="period-close-note"
            type="text"
            value={closeReason}
            onChange={e => setCloseReason(e.target.value)}
            placeholder={t("close.reasonPlaceholder")}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)",
              color: "var(--color-content-primary)",
              fontSize: "0.875rem",
              boxSizing: "border-box"
            }}
          />
        </div>

        {closeMutation.isError && (
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
            {t("close.executeError")}
          </div>
        )}
      </div>
    </Dialog>
  );
};
