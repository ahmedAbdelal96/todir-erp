import React, { useState } from "react";
import {
  usePreviewFxRevaluationMutation,
  usePostFxRevaluationMutation,
  useReverseFxRevaluationMutation
} from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { useAccountingI18n } from "../../i18n/index.ts";
import type { FxRevaluationRunView, FxRevaluationLineView } from "../../types.ts";

export interface FxRevaluationViewProps {
  readonly legalEntityId?: string;
}

export const FxRevaluationView: React.FC<FxRevaluationViewProps> = ({ legalEntityId }) => {
  const { t } = useAccountingI18n();

  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [autoReversalDate, setAutoReversalDate] = useState<string>("");
  const [currentRun, setCurrentRun] = useState<FxRevaluationRunView | null>(null);

  // Reverse Modal State
  const [isReverseModalOpen, setIsReverseModalOpen] = useState(false);
  const [reversalDate, setReversalDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [reversalReason, setReversalReason] = useState<string>("");

  // Mutations
  const previewMutation = usePreviewFxRevaluationMutation(legalEntityId);
  const postMutation = usePostFxRevaluationMutation(legalEntityId);
  const reverseMutation = useReverseFxRevaluationMutation(legalEntityId);

  const handlePreview = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await previewMutation.mutateAsync({
        asOfDate,
        idempotencyKey: `fx-prev-${Date.now()}`,
        autoReversalDate: autoReversalDate || null
      });
      setCurrentRun(res);
    } catch {
      // Handled by previewMutation.isError
    }
  };

  const handlePost = async () => {
    if (!currentRun) return;
    try {
      const res = await postMutation.mutateAsync(currentRun.id);
      setCurrentRun(res);
    } catch {
      // Handled by postMutation.isError
    }
  };

  const handleReverse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentRun || !reversalReason.trim()) return;
    try {
      const res = await reverseMutation.mutateAsync({
        runId: currentRun.id,
        request: {
          reversalDate,
          reason: reversalReason.trim()
        }
      });
      setCurrentRun(res);
      setIsReverseModalOpen(false);
    } catch {
      // Handled by reverseMutation.isError
    }
  };

  const totalGainLoss = (currentRun?.lines ?? []).reduce(
    (sum, line) => sum + (line.differenceAmount || 0),
    0
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="fx-revaluation-view">
      {/* Top Header & Parameters Card */}
      <Surface variant="card" style={{ padding: "1.25rem 1.5rem" }}>
        <div style={{ marginBottom: "1.25rem" }}>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
            {t("fx.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
            {t("fx.subtitle")}
          </p>
        </div>

        <form onSubmit={handlePreview} style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-end", gap: "1rem" }}>
          <div>
            <label htmlFor="fx-as-of" style={{ display: "block", marginBottom: "0.375rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-secondary, #334155)" }}>
              {t("fx.asOfDateLabel")}
            </label>
            <input
              id="fx-as-of"
              type="date"
              value={asOfDate}
              onChange={(e) => setAsOfDate(e.target.value)}
              required
              style={{
                fontSize: "0.875rem",
                padding: "0.4375rem 0.625rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                color: "var(--color-content-primary, #0f172a)"
              }}
            />
          </div>

          <div>
            <label htmlFor="fx-auto-rev" style={{ display: "block", marginBottom: "0.375rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-secondary, #334155)" }}>
              {t("fx.autoReversalDateLabel")} ({t("fx.optional")})
            </label>
            <input
              id="fx-auto-rev"
              type="date"
              value={autoReversalDate}
              onChange={(e) => setAutoReversalDate(e.target.value)}
              style={{
                fontSize: "0.875rem",
                padding: "0.4375rem 0.625rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                color: "var(--color-content-primary, #0f172a)"
              }}
            />
          </div>

          <div>
            <Button
              variant="primary"
              isLoading={previewMutation.isPending}
              onPress={handlePreview as unknown as () => void}
            >
              {t("fx.btnPreview")}
            </Button>
          </div>
        </form>

        {previewMutation.isError && (
          <div style={{ marginTop: "1rem", padding: "0.75rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)", border: "1px solid var(--color-status-danger, #dc2626)", color: "var(--color-status-danger, #dc2626)", fontSize: "0.8125rem" }}>
            {t("fx.previewError")}
          </div>
        )}
      </Surface>

      {/* Revaluation Lines & Actions View */}
      {currentRun && (
        <Surface
          variant="card"
          title={t("fx.previewLinesTitle")}
          description={`${currentRun.asOfDate} — ${t("fx.totalItems")}: ${currentRun.lines.length}`}
          headerActions={
            <div style={{ display: "flex", alignItems: "center", gap: "1rem", flexWrap: "wrap" }}>
              <div style={{ textAlign: "end" }}>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", display: "block" }}>
                  {t("fx.totalGainLoss")}
                </span>
                <MoneyDisplay
                  value={{ amount: totalGainLoss.toFixed(2), currency: "EGP" }}
                  size="large"
                  coloredSign
                />
              </div>

              {currentRun.status === "Preview" && currentRun.lines.length > 0 && (
                <Button
                  variant="primary"
                  isLoading={postMutation.isPending}
                  onPress={handlePost}
                >
                  ✓ {t("fx.btnPost")}
                </Button>
              )}

              {currentRun.status === "Posted" && (
                <Button
                  variant="danger"
                  onPress={() => setIsReverseModalOpen(true)}
                >
                  {t("fx.btnReverse")}
                </Button>
              )}
            </div>
          }
          style={{ padding: "0" }}
        >
          {postMutation.isError && (
            <div style={{ padding: "0.75rem 1rem", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)", borderBottom: "1px solid var(--color-status-danger, #dc2626)", color: "var(--color-status-danger, #dc2626)", fontSize: "0.8125rem" }}>
              {t("fx.postError")}
            </div>
          )}

          {currentRun.lines.length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              <p style={{ margin: 0 }}>{t("fx.noOpenItemsToRevalue")}</p>
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                <thead>
                  <tr
                    style={{
                      backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                      borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                      color: "var(--color-content-secondary, #334155)",
                      fontSize: "0.8125rem",
                      fontWeight: 700
                    }}
                  >
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("fx.colDirection")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("fx.colTxAmount")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("fx.colCarryingAmount")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("fx.colRate")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("fx.colRevaluedAmount")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("fx.colDifference")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("fx.colRole")}</th>
                  </tr>
                </thead>
                <tbody>
                  {currentRun.lines.map((line: FxRevaluationLineView, idx: number) => (
                    <tr
                      key={line.id}
                      style={{
                        borderBottom: idx < currentRun.lines.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                        backgroundColor: idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)"
                      }}
                    >
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <StatusBadge
                          intent={line.direction === "Receivable" ? "info" : "warning"}
                          label={line.direction}
                          size="small"
                        />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <MoneyDisplay value={{ amount: line.openTransactionAmount.toFixed(2), currency: line.transactionCurrency }} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <MoneyDisplay value={{ amount: line.carryingBaseAmount.toFixed(2), currency: line.baseCurrency }} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
                        {line.exchangeRate.toFixed(4)}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        <MoneyDisplay value={{ amount: line.revaluedBaseAmount.toFixed(2), currency: line.baseCurrency }} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                        <MoneyDisplay
                          value={{ amount: line.differenceAmount.toFixed(2), currency: line.baseCurrency }}
                          coloredSign
                        />
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        <span style={{ fontSize: "0.75rem", padding: "0.125rem 0.375rem", borderRadius: "var(--radius-sm, 0.25rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", border: "1px solid var(--color-border-subtle, #e2e8f0)", color: "var(--color-content-secondary, #334155)" }}>
                          {line.differenceRole}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* REVERSAL MODAL (Using Approved Dialog Primitive) */}
      <Dialog
        isOpen={isReverseModalOpen}
        onOpenChange={(open) => !open && setIsReverseModalOpen(false)}
        title={t("fx.modalReverseTitle")}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsReverseModalOpen(false)}>
              {t("fx.modalCancel")}
            </Button>
            <Button
              variant="danger"
              isLoading={reverseMutation.isPending}
              onPress={handleReverse as unknown as () => void}
            >
              {t("fx.modalConfirmReverse")}
            </Button>
          </div>
        }
      >
        <form onSubmit={handleReverse} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label htmlFor="fx-rev-date" style={{ display: "block", marginBottom: "0.375rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("fx.modalReversalDateLabel")}
            </label>
            <input
              id="fx-rev-date"
              type="date"
              value={reversalDate}
              onChange={(e) => setReversalDate(e.target.value)}
              required
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                fontSize: "0.875rem"
              }}
            />
          </div>

          <div>
            <label htmlFor="fx-rev-reason" style={{ display: "block", marginBottom: "0.375rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("fx.modalReversalReasonLabel")} *
            </label>
            <textarea
              id="fx-rev-reason"
              rows={3}
              value={reversalReason}
              onChange={(e) => setReversalReason(e.target.value)}
              placeholder={t("fx.modalReversalReasonPlaceholder")}
              required
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                fontSize: "0.875rem",
                resize: "vertical"
              }}
            />
          </div>

          {reverseMutation.isError && (
            <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)", border: "1px solid var(--color-status-danger, #dc2626)", color: "var(--color-status-danger, #dc2626)", fontSize: "0.8125rem" }}>
              {t("fx.modalReverseError")}
            </div>
          )}
        </form>
      </Dialog>
    </div>
  );
};
