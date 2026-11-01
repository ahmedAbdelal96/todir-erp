import React, { useState } from "react";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { useApproveGoLiveMutation } from "../../hooks/use-accounting-queries.ts";
import { useAccountingI18n } from "../../i18n";
import type { GoLiveApprovalView, CloseCheckView } from "../../types.ts";

export interface ApproveGoLiveModalProps {
  readonly openingBatchId: string;
  readonly goLiveDate?: string | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSuccess?: () => void;
}

export const ApproveGoLiveModal: React.FC<ApproveGoLiveModalProps> = ({
  openingBatchId,
  goLiveDate,
  isOpen,
  onClose,
  onSuccess
}) => {
  const { t } = useAccountingI18n();
  const [reason, setReason] = useState("");
  const [subledgerBatchId, setSubledgerBatchId] = useState("");
  const [approvalResult, setApprovalResult] = useState<GoLiveApprovalView | null>(null);

  const approveMutation = useApproveGoLiveMutation();

  if (!isOpen) return null;

  const handleApprove = async () => {
    if (!reason.trim()) return;
    try {
      const res = await approveMutation.mutateAsync({
        openingBatchId,
        subledgerOpeningBatchId: subledgerBatchId.trim() || null,
        financialGoLiveDate: goLiveDate || null,
        reason: reason.trim(),
        idempotencyKey: `go-live-approve-${openingBatchId}-${Date.now()}`
      });
      setApprovalResult(res);
      onSuccess?.();
    } catch {
      // Handled by mutation error
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("goLive.modalTitle")}
      size="medium"
      footer={
        approvalResult ? (
          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="primary" onPress={onClose}>
              {t("goLive.closeDone")}
            </Button>
          </div>
        ) : (
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={onClose}>
              {t("goLive.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={approveMutation.isPending}
              isDisabled={approveMutation.isPending || !reason.trim()}
              onPress={handleApprove}
              data-testid="btn-confirm-go-live"
            >
              {approveMutation.isPending ? t("goLive.approving") : t("goLive.btnApprove")}
            </Button>
          </div>
        )
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="approve-go-live-modal">
        <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("goLive.batchLabel")}: <span style={{ fontFamily: "monospace" }}>{openingBatchId}</span> | {t("goLive.dateLabel")}: {goLiveDate || "—"}
        </div>

        {approvalResult ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "rgba(16, 185, 129, 0.1)",
                border: "1px solid rgba(16, 185, 129, 0.3)",
                color: "var(--color-status-success, #059669)",
                display: "flex",
                alignItems: "center",
                gap: "0.75rem"
              }}
            >
              <span style={{ fontSize: "1.5rem" }}>✓</span>
              <div>
                <h4 style={{ margin: 0, fontWeight: 700, fontSize: "0.875rem" }}>{t("goLive.successTitle")}</h4>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem" }}>{t("goLive.successSubtitle")}</p>
              </div>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <span style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-secondary)", textTransform: "uppercase" }}>
                {t("goLive.executedChecksTitle")}
              </span>
              <div
                style={{
                  maxHeight: "12rem",
                  overflowY: "auto",
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "var(--color-surface-card)"
                }}
              >
                {approvalResult.checks.map((check: CloseCheckView, idx) => (
                  <div
                    key={idx}
                    style={{
                      padding: "0.625rem 0.875rem",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: "0.5rem",
                      borderBottom: idx < approvalResult.checks.length - 1 ? "1px solid var(--color-border-subtle, #f1f5f9)" : "none"
                    }}
                  >
                    <div>
                      <span style={{ fontWeight: 600, color: "var(--color-content-primary)", fontSize: "0.8125rem" }}>
                        {check.code}
                      </span>
                      <p style={{ margin: "0.125rem 0 0", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>
                        {check.message}
                      </p>
                    </div>
                    <StatusBadge
                      label={check.status}
                      intent={check.status === "Pass" ? "success" : "neutral"}
                    />
                  </div>
                ))}
              </div>
            </div>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                padding: "0.75rem 1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                backgroundColor: "rgba(59, 130, 246, 0.1)",
                border: "1px solid rgba(59, 130, 246, 0.3)",
                color: "var(--color-brand-primary, #1d4ed8)",
                fontSize: "0.8125rem"
              }}
            >
              ℹ️ {t("goLive.infoBanner")}
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>
              <label htmlFor="go-live-subledger-batch" style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("goLive.subledgerBatchLabel")} ({t("goLive.optional")})
              </label>
              <input
                id="go-live-subledger-batch"
                type="text"
                value={subledgerBatchId}
                onChange={e => setSubledgerBatchId(e.target.value)}
                placeholder="UUID"
                style={{
                  width: "100%",
                  padding: "0.5rem 0.75rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-card)",
                  color: "var(--color-content-primary)",
                  fontSize: "0.8125rem",
                  fontFamily: "monospace",
                  boxSizing: "border-box"
                }}
              />
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>
              <label htmlFor="go-live-approval-reason" style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("goLive.reasonLabel")} *
              </label>
              <textarea
                id="go-live-approval-reason"
                required
                rows={3}
                value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder={t("goLive.reasonPlaceholder")}
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
            </div>

            {approveMutation.isError && (
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
                {t("goLive.errorApproving")}
              </div>
            )}
          </div>
        )}
      </div>
    </Dialog>
  );
};
