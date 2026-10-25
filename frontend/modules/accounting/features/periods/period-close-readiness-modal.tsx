import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { usePeriodCloseReadiness, useClosePeriod } from "../../hooks/use-ledger-queries.ts";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";

export interface PeriodCloseReadinessModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly periodId: string | null;
  readonly periodCode?: string | undefined;
}

function formatPeriodCheckItem(
  reason: { code?: string; message: string },
  t: (key: string) => string
): string {
  const msg = reason.message || "";
  if (reason.code === "CHK1" || /all transactions for period/i.test(msg)) {
    return t("period.allTransactionsPosted");
  }
  if (reason.code === "ledger.readiness" || /ledger readiness is evaluated/i.test(msg)) {
    return t("period.ledgerReadinessEvaluated");
  }
  if (reason.code === "ledger.period_not_found" || /period.*not found/i.test(msg)) {
    return t("period.periodNotFound");
  }
  if (reason.code === "ledger.period_already_closed" || /already closed/i.test(msg)) {
    return t("period.periodAlreadyClosed");
  }
  return msg;
}

export function PeriodCloseReadinessModal({
  isOpen,
  onClose,
  legalEntityId,
  periodId,
  periodCode
}: PeriodCloseReadinessModalProps): React.JSX.Element | null {
  const { t } = useTranslation("accounting");
  const { data: readiness, isLoading } = usePeriodCloseReadiness(legalEntityId, periodId ?? "");
  const closePeriodMutation = useClosePeriod();

  if (!periodId || !isOpen) return null;

  const handleClosePeriod = async () => {
    await closePeriodMutation.mutateAsync({ legalEntityId, periodId });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={periodCode ? `${t("period.closeReadinessTitle")}: ${periodCode}` : t("period.closeReadinessTitle")}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.close")}
          </Button>
          {readiness?.ready && (
            <Button
              variant="primary"
              isLoading={closePeriodMutation.isPending}
              onPress={handleClosePeriod}
            >
              {t("period.proceedToClose")}
            </Button>
          )}
        </div>
      }
    >
      {isLoading || !readiness ? (
        <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <span style={{ fontWeight: 600 }}>{t("period.overallReadiness")}:</span>
            <StatusBadge
              status={readiness.ready ? "success" : "danger"}
              label={readiness.ready ? t("period.readyToClose") : t("period.closeBlocked")}
            />
          </div>

          {/* Checks passed */}
          {readiness.checks.length > 0 && (
            <div>
              <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-status-success)" }}>
                ✓ {t("period.passedChecks")} ({readiness.checks.length})
              </h4>
              <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
                {readiness.checks.map((c, i) => (
                  <li key={`check-${i}`} style={{ marginBottom: "0.25rem" }}>
                    <span>{formatPeriodCheckItem(c, t as any)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Warnings */}
          {readiness.warnings.length > 0 && (
            <div>
              <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-status-warning)" }}>
                ⚠ {t("period.warnings")} ({readiness.warnings.length})
              </h4>
              <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
                {readiness.warnings.map((w, i) => (
                  <li key={`warn-${i}`} style={{ marginBottom: "0.25rem" }}>
                    <span>{formatPeriodCheckItem(w, t as any)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* Blockers */}
          {readiness.blockers.length > 0 && (
            <div>
              <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", color: "var(--color-status-danger)" }}>
                ✕ {t("period.blockers")} ({readiness.blockers.length})
              </h4>
              <ul style={{ margin: 0, paddingInlineStart: "1.25rem", fontSize: "0.875rem", color: "var(--color-status-danger)" }}>
                {readiness.blockers.map((b, i) => (
                  <li key={`block-${i}`} style={{ marginBottom: "0.25rem" }}>
                    <span>{formatPeriodCheckItem(b, t as any)}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}
