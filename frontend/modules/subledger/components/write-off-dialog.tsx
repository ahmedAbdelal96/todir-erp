import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { useCreateWriteOffMutation } from "../hooks/subledger-queries.ts";
import type { PartyOpenItem } from "../types.ts";

export interface WriteOffDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly item: PartyOpenItem | null | undefined;
  readonly onSuccess?: () => void;
  readonly onCompleted?: () => void;
}

export function WriteOffDialog({
  isOpen,
  onClose,
  legalEntityId,
  item,
  onSuccess,
  onCompleted
}: WriteOffDialogProps): React.JSX.Element | null {
  const { t } = useTranslation("subledger");
  const [amount, setAmount] = useState(() => item?.openTransactionAmount || "");
  const [reasonCode, setReasonCode] = useState("Tolerance");
  const [reasonText, setReasonText] = useState("");
  const [effectiveDate, setEffectiveDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const writeOffMutation = useCreateWriteOffMutation();

  if (!item) return null;

  const handleSubmit = async () => {
    if (!amount || parseFloat(amount) <= 0) {
      setError(t("writeOff.remainingAmount"));
      return;
    }
    setError(null);

    try {
      await writeOffMutation.mutateAsync({
        legalEntityId,
        openItemId: item.id,
        amount,
        reasonCode,
        reasonText: reasonText.trim() || reasonCode,
        effectiveDate,
        idempotencyKey: crypto.randomUUID()
      });
      if (onSuccess) onSuccess();
      if (onCompleted) onCompleted();
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="medium"
      title={t("writeOff.title")}
      description={item.sourceDisplayNumber || item.sourceReference}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", width: "100%" }}>
          <Button variant="secondary" onClick={onClose} isDisabled={writeOffMutation.isPending}>
            {t("reversal.cancelButton")}
          </Button>
          <Button
            variant="danger"
            onClick={() => void handleSubmit()}
            isLoading={writeOffMutation.isPending}
            isDisabled={!amount || parseFloat(amount) <= 0}
          >
            {t("writeOff.submitButton")}
          </Button>
        </div>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
        <Alert variant="warning" title={t("writeOff.notice")}>
          {t("writeOff.notice")}
        </Alert>

        {error && <Alert variant="danger">{error}</Alert>}

        {/* Current Remaining Display */}
        <div
          style={{
            padding: "0.75rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            borderRadius: "var(--radius-md, 0.375rem)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center"
          }}
        >
          <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
            {t("openItems.remainingAmount")}:
          </span>
          <span style={{ fontSize: "1.125rem", fontWeight: 800, color: "var(--color-status-danger, #dc2626)" }}>
            <MoneyDisplay
              value={{ amount: item.openTransactionAmount, currency: item.transactionCurrency }}
              displayCurrencyCode
            />
          </span>
        </div>

        {/* Write-Off Amount */}
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("writeOff.remainingAmount")} ({item.transactionCurrency}) *
          </label>
          <input
            type="number"
            step="0.01"
            min="0.01"
            max={item.openTransactionAmount}
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem"
            }}
          />
        </div>

        {/* Reason Code */}
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("writeOff.reasonCode")} *
          </label>
          <select
            value={reasonCode}
            onChange={(e) => setReasonCode(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem",
              backgroundColor: "#ffffff"
            }}
          >
            <option value="Tolerance">{t("writeOff.reasons.Tolerance")}</option>
            <option value="BadDebt">{t("writeOff.reasons.BadDebt")}</option>
            <option value="DisputeSettlement">{t("writeOff.reasons.DisputeSettlement")}</option>
            <option value="MinorBalance">{t("writeOff.reasons.MinorBalance")}</option>
          </select>
        </div>

        {/* Reason Text */}
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("writeOff.reasonText")}
          </label>
          <textarea
            value={reasonText}
            onChange={(e) => setReasonText(e.target.value)}
            rows={2}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem",
              fontFamily: "inherit"
            }}
          />
        </div>

        {/* Effective Date */}
        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, marginBottom: "0.375rem" }}>
            {t("reversal.effectiveDate")} *
          </label>
          <input
            type="date"
            value={effectiveDate}
            onChange={(e) => setEffectiveDate(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              fontSize: "0.875rem"
            }}
          />
        </div>
      </div>
    </Dialog>
  );
}
