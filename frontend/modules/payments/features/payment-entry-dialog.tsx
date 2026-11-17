import React, { useState, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { PartyPicker } from "../../directory/public.ts";
import { useSubmitPayment } from "../hooks/payment-queries.ts";
import type { PaymentCommand, PaymentMethod, PaymentResult } from "../types.ts";
import type { CurrencyCode } from "../../../components/contracts/domain-values.ts";

export interface PaymentEntryDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly defaultPartyId?: string | undefined;
  readonly defaultPartyName?: string | undefined;
  readonly onPaymentComplete?: ((result: PaymentResult) => void) | undefined;
}

export function PaymentEntryDialog({
  isOpen,
  onOpenChange,
  defaultPartyId,
  defaultPartyName,
  onPaymentComplete
}: PaymentEntryDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");
  const [selectedPartyId, setSelectedPartyId] = useState(defaultPartyId ?? "");
  const [selectedPartyName, setSelectedPartyName] = useState(defaultPartyName ?? "");
  const [amountValue, setAmountValue] = useState<{ amount: string; currency: CurrencyCode }>({
    amount: "5000.00",
    currency: "SAR"
  });
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("BankTransfer");
  const [sourceAccount, setSourceAccount] = useState("1010-البنك الأهلي التجاري");
  const [destinationAccount, setDestinationAccount] = useState("2010-حساب المورد الجاري");
  const [referenceNumber, setReferenceNumber] = useState("PAY-REF-99201");
  const [notes, setNotes] = useState("");

  const [step, setStep] = useState<"form" | "confirming" | "unknown_outcome" | "success">("form");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastPaymentResult, setLastPaymentResult] = useState<PaymentResult | null>(null);

  const idempotencyKeyRef = useRef<string>(crypto.randomUUID());
  const submitPaymentMutation = useSubmitPayment();

  const handleOpenConfirmation = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!selectedPartyId) {
      setErrorMessage(t("errors.party_required"));
      return;
    }
    if (!amountValue.amount || amountValue.amount === "0") {
      setErrorMessage(t("entry.invalidAmount"));
      return;
    }
    setStep("confirming");
  };

  const handleExecutePayment = async () => {
    setErrorMessage(null);
    const command: PaymentCommand = {
      partyId: selectedPartyId,
      partyName: selectedPartyName,
      amount: amountValue.amount,
      currency: amountValue.currency,
      paymentMethod,
      referenceNumber,
      sourceAccount,
      destinationAccount,
      notes: notes || undefined
    };

    try {
      const result = await submitPaymentMutation.mutateAsync({
        command,
        idempotencyKey: idempotencyKeyRef.current
      });

      setLastPaymentResult(result);
      setStep("success");
    } catch (err: any) {
      // In network failure / timeout during high-risk money transfer, enter safe Unknown Outcome state
      if (err?.name === "TypeError" || err?.message?.includes("fetch") || err?.status >= 500) {
        setStep("unknown_outcome");
      } else {
        setErrorMessage(t("entry.submitFailed"));
        setStep("form");
      }
    }
  };

  const handleClose = () => {
    if (step === "success" && lastPaymentResult) {
      onPaymentComplete?.(lastPaymentResult);
    }
    setStep("form");
    setErrorMessage(null);
    idempotencyKeyRef.current = crypto.randomUUID();
    onOpenChange(false);
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!submitPaymentMutation.isPending) {
          if (!open) handleClose();
          else onOpenChange(true);
        }
      }}
      title={
        step === "confirming"
          ? t("entry.confirmTitle")
          : step === "unknown_outcome"
          ? t("entry.unknownOutcomeTitle")
          : step === "success"
          ? t("entry.successTitle")
          : t("entry.title")
      }
      description={
        step === "confirming"
          ? t("entry.confirmDescription")
          : t("entry.description", { name: selectedPartyName })
      }
      size="medium"
    >
      {errorMessage && (
        <div
          style={{
            marginBottom: "1rem",
            padding: "0.75rem",
            backgroundColor: "var(--color-surface-danger-subtle)",
            color: "var(--color-status-danger)",
            borderRadius: "var(--radius-md)",
            fontSize: "0.875rem"
          }}
          role="alert"
        >
          {errorMessage}
        </div>
      )}

      {step === "form" && (
        <form onSubmit={handleOpenConfirmation} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <PartyPicker
            label={t("entry.payee")}
            value={selectedPartyId || undefined}
            onChange={(partyId, party) => {
              setSelectedPartyId(partyId);
              setSelectedPartyName(party?.displayName ?? "");
            }}
            enableQuickCreate={false}
            isRequired
          />

          <MoneyField
            label={t("entry.amount")}
            value={amountValue}
            onChange={setAmountValue}
            isRequired
          />

          <Select
            label={t("entry.paymentMethod")}
            selectedKey={paymentMethod}
            onSelectionChange={(k) => setPaymentMethod(k as PaymentMethod)}
            options={[
              { id: "BankTransfer", label: t("entry.bankTransfer") },
              { id: "Cash", label: t("entry.cash") },
              { id: "Check", label: t("entry.check") },
              { id: "Card", label: t("entry.card") }
            ]}
          />

          <TextField
            label={t("entry.sourceAccount")}
            value={sourceAccount}
            onChange={setSourceAccount}
            isRequired
          />

          <TextField
            label={t("entry.destinationAccount")}
            value={destinationAccount}
            onChange={setDestinationAccount}
            isRequired
          />

          <TextField
            label={t("entry.referenceNumber")}
            value={referenceNumber}
            onChange={setReferenceNumber}
            isRequired
          />

          <TextField
            label={t("entry.notes")}
            value={notes}
            onChange={setNotes}
          />

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
            <Button variant="secondary" onPress={handleClose}>
              {t("entry.cancel")}
            </Button>
            <Button variant="primary" type="submit">
              {t("entry.continueToConfirmation")}
            </Button>
          </div>
        </form>
      )}

      {step === "confirming" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div
            style={{
              padding: "1rem",
              backgroundColor: "var(--color-surface-subtle)",
              borderRadius: "var(--radius-md)",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid var(--color-border-default)", paddingBottom: "0.5rem" }}>
              <span style={{ color: "var(--color-content-muted)" }}>{t("entry.payee")}</span>
              <span style={{ fontWeight: 600 }}>{selectedPartyName || "—"}</span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid var(--color-border-default)", paddingBottom: "0.5rem" }}>
              <span style={{ color: "var(--color-content-muted)" }}>{t("entry.totalAmount")}</span>
              <span style={{ fontWeight: 700, fontSize: "1.125rem", color: "var(--color-brand-primary)" }}>
                <MoneyDisplay value={{ amount: amountValue.amount, currency: amountValue.currency }} />
              </span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid var(--color-border-default)", paddingBottom: "0.5rem" }}>
              <span style={{ color: "var(--color-content-muted)" }}>{t("entry.sourceAccountLabel")}</span>
              <span>{sourceAccount}</span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between" }}>
              <span style={{ color: "var(--color-content-muted)" }}>{t("entry.referenceLabel")}</span>
              <span style={{ fontFamily: "var(--font-mono)" }}>{referenceNumber}</span>
            </div>
          </div>

          <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("entry.irreversibleWarning")}
          </p>

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem" }}>
            <Button
              variant="secondary"
              onPress={() => setStep("form")}
              isDisabled={submitPaymentMutation.isPending}
            >
              {t("entry.editDetails")}
            </Button>
            <Button
              variant="primary"
              onPress={handleExecutePayment}
              isDisabled={submitPaymentMutation.isPending}
            >
              {submitPaymentMutation.isPending ? t("entry.submitting") : t("entry.confirmAndSend")}
            </Button>
          </div>
        </div>
      )}

      {step === "unknown_outcome" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div
            style={{
              padding: "1rem",
              backgroundColor: "var(--color-surface-warning-subtle, #fef3c7)",
              color: "var(--color-status-warning, #b45309)",
              borderRadius: "var(--radius-md)",
              fontSize: "0.875rem"
            }}
          >
            <strong>{t("entry.connectionInterrupted")}</strong>
            <p style={{ margin: "0.5rem 0 0 0" }}>
              {t("entry.unknownOutcomeDetails")}
            </p>
          </div>
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
            <Button variant="primary" onPress={handleClose}>
              {t("entry.closeAndCheckStatus")}
            </Button>
          </div>
        </div>
      )}

      {step === "success" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", textAlign: "center", padding: "1rem 0" }}>
          <div style={{ fontSize: "2rem" }}>✓</div>
          <h3 style={{ margin: 0 }}>{t("entry.successMessage")}</h3>
          <p style={{ color: "var(--color-content-muted)", margin: 0 }}>
            {t("entry.paymentReference")}: {lastPaymentResult?.paymentId || referenceNumber}
          </p>
          <div style={{ marginTop: "1rem" }}>
            <Button variant="primary" onPress={handleClose}>
              {t("entry.done")}
            </Button>
          </div>
        </div>
      )}
    </Dialog>
  );
}
