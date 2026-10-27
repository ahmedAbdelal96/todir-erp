import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { useApplyPayment } from "../hooks/payment-queries.ts";
import type { PaymentRecord, ApplyPaymentRequest } from "../types.ts";

export interface ApplyPaymentDialogProps {
  readonly payment: PaymentRecord | null;
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly onSuccess?: () => void;
}

export function ApplyPaymentDialog({
  payment,
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  onSuccess
}: ApplyPaymentDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [targetOpenItemId, setTargetOpenItemId] = useState<string>("");
  const [amount, setAmount] = useState<string>(payment?.amount || "");
  const [applicationDate, setApplicationDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [errorMsg, setErrorMsg] = useState<string>("");

  const applyMutation = useApplyPayment();

  if (!payment) return <></>;

  const handleClose = () => {
    setErrorMsg("");
    setTargetOpenItemId("");
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    if (!targetOpenItemId.trim()) {
      setErrorMsg(t("apply.targetItemRequired"));
      return;
    }

    const numAmount = Number(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    try {
      const request: ApplyPaymentRequest = {
        applicationDate,
        settlementDate: applicationDate,
        lines: [
          {
            targetOpenItemId: targetOpenItemId.trim(),
            amount: Number(amount).toFixed(2)
          }
        ]
      };

      await applyMutation.mutateAsync({
        legalEntityId,
        paymentId: payment.id,
        request,
        branchId
      });

      handleClose();
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const code = problem?.problemDetails?.code;
      if (code && (t as any)(`errors.${code}`, { defaultValue: "" })) {
        setErrorMsg((t as any)(`errors.${code}`));
      } else {
        setErrorMsg((err as Error).message || t("errors.unknown"));
      }
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={t("apply.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "6px",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#991b1b",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ backgroundColor: "#eff6ff", border: "1px solid #bfdbfe", borderRadius: "6px", padding: "0.75rem", fontSize: "0.8125rem", color: "#1e40af" }}>
          {t("apply.notice")}
        </div>

        {/* Payment Summary */}
        <div
          style={{
            backgroundColor: "#f8fafc",
            border: "1px solid #cbd5e1",
            borderRadius: "6px",
            padding: "0.875rem 1rem",
            fontSize: "0.875rem"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.25rem" }}>
            <span style={{ color: "#64748b" }}>{t("apply.paymentNumber")}:</span>
            <span style={{ fontWeight: 700 }}><bdi>{payment.number}</bdi></span>
          </div>
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <span style={{ color: "#64748b" }}>{t("apply.totalAmount")}:</span>
            <span style={{ fontWeight: 700 }}>
              <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
            </span>
          </div>
        </div>

        {/* Target Open Item */}
        <TextField
          label={t("apply.targetOpenItemLabel")}
          value={targetOpenItemId}
          onChange={setTargetOpenItemId}
          placeholder={t("apply.targetOpenItemPlaceholder")}
          isRequired
        />

        {/* Amount to Apply */}
        <TextField
          label={t("apply.amountLabel")}
          value={amount}
          onChange={setAmount}
          placeholder="0.00"
          isRequired
          type="number"
        />

        {/* Application Date */}
        <TextField
          label={t("apply.dateLabel")}
          type="date"
          value={applicationDate}
          onChange={setApplicationDate}
          isRequired
        />

        {/* Actions */}
        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: "0.75rem",
            marginTop: "1rem",
            borderTop: "1px solid #e2e8f0",
            paddingTop: "1rem"
          }}
        >
          <Button variant="secondary" onPress={handleClose} isDisabled={applyMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={applyMutation.isPending}>
            {applyMutation.isPending ? t("apply.submitting") : t("apply.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
