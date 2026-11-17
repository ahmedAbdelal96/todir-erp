import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { AccountPicker } from "../components/account-picker.tsx";
import { PaymentCurrencyPicker } from "../components/payment-currency-picker.tsx";
import { useCreateInternalTransfer } from "../hooks/payment-queries.ts";
import type { PaymentAccount, CreateInternalTransferRequest } from "../types.ts";

export interface InternalTransferDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly accounts: readonly PaymentAccount[];
  readonly onSuccess?: () => void;
}

export function InternalTransferDialog({
  isOpen,
  onOpenChange,
  legalEntityId,
  accounts,
  onSuccess
}: InternalTransferDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [sourceAccountId, setSourceAccountId] = useState<string>("");
  const [destinationAccountId, setDestinationAccountId] = useState<string>("");
  const [amount, setAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<string>("EGP");
  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [reference, setReference] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const createTransfer = useCreateInternalTransfer();

  const sourceAccount = accounts.find((a) => a.id === sourceAccountId);
  const destinationAccount = accounts.find((a) => a.id === destinationAccountId);

  const isCrossCurrency =
    Boolean(sourceAccount?.currencyCode && destinationAccount?.currencyCode) &&
    sourceAccount?.currencyCode !== destinationAccount?.currencyCode;

  const resetForm = () => {
    setSourceAccountId("");
    setDestinationAccountId("");
    setAmount("");
    setCurrencyCode("EGP");
    setPaymentDate(new Date().toISOString().slice(0, 10));
    setReference("");
    setErrorMsg("");
  };

  const handleClose = () => {
    resetForm();
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    if (!sourceAccountId || !destinationAccountId) {
      setErrorMsg(t("errors.accounts_required"));
      return;
    }

    if (sourceAccountId === destinationAccountId) {
      setErrorMsg(t("errors.same_source_destination"));
      return;
    }

    const numAmount = Number(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    try {
      const request: CreateInternalTransferRequest = {
        legalEntityId,
        sourcePaymentAccountId: sourceAccountId,
        destinationPaymentAccountId: destinationAccountId,
        amount: Number(amount).toFixed(2),
        currencyCode: (sourceAccount?.currencyCode || currencyCode) as any,
        paymentDate,
        postingDate: paymentDate,
        reference: reference.trim() || null,
        destinationCurrencyCode: isCrossCurrency ? destinationAccount?.currencyCode : null
      };

      await createTransfer.mutateAsync({
        legalEntityId,
        request
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
      title={t("transfer.title")}
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
          {t("transfer.notice")}
        </div>

        {/* Source & Destination Accounts */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <AccountPicker
            label={t("transfer.sourceAccountLabel")}
            accounts={accounts}
            selectedId={sourceAccountId}
            onSelect={(acc) => {
              setSourceAccountId(acc?.id || "");
              if (acc?.currencyCode) setCurrencyCode(acc.currencyCode);
            }}
            isRequired
          />

          <AccountPicker
            label={t("transfer.destinationAccountLabel")}
            accounts={accounts}
            selectedId={destinationAccountId}
            onSelect={(acc) => setDestinationAccountId(acc?.id || "")}
            isRequired
          />
        </div>

        {/* Cross-Currency Banner */}
        {isCrossCurrency && (
          <div style={{ backgroundColor: "#fefce8", border: "1px solid #fef08a", borderRadius: "6px", padding: "0.75rem", fontSize: "0.8125rem", color: "#854d0e" }}>
            <strong>{t("transfer.crossCurrencyNotice")}: </strong>
            {(t as any)("transfer.crossCurrencyDesc", {
              source: sourceAccount?.currencyCode,
              dest: destinationAccount?.currencyCode
            })}
          </div>
        )}

        {/* Amount & Currency */}
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("transfer.amountLabel")}
            value={amount}
            onChange={setAmount}
            placeholder="0.00"
            isRequired
            type="number"
          />
          <PaymentCurrencyPicker
            legalEntityId={legalEntityId}
            label={t("transfer.currencyLabel")}
            value={sourceAccount?.currencyCode || currencyCode}
            onChange={setCurrencyCode}
            isRequired
            isDisabled={Boolean(sourceAccount?.currencyCode)}
          />
        </div>

        {/* Date & Reference */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("transfer.dateLabel")}
            value={paymentDate}
            onChange={setPaymentDate}
            isRequired
          />
          <TextField
            label={t("transfer.referenceLabel")}
            value={reference}
            onChange={setReference}
            placeholder={t("transfer.referencePlaceholder")}
          />
        </div>

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
          <Button variant="secondary" onPress={handleClose} isDisabled={createTransfer.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createTransfer.isPending}>
            {createTransfer.isPending ? t("transfer.submitting") : t("transfer.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
