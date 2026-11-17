import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { PartyPicker } from "../../directory/public.ts";
import { AccountPicker } from "../components/account-picker.tsx";
import { PaymentCurrencyPicker } from "../components/payment-currency-picker.tsx";
import {
  useCreateOutgoingPayment,
  useCompleteManualPayment
} from "../hooks/payment-queries.ts";
import type {
  PaymentAccount,
  PaymentMethod,
  PaymentType,
  CreatePaymentRequest
} from "../types.ts";

export interface PaySupplierDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly accounts: readonly PaymentAccount[];
  readonly onSuccess?: () => void;
}

export function PaySupplierDialog({
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  accounts,
  onSuccess
}: PaySupplierDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [partyId, setPartyId] = useState<string>("");
  const [isAdvance, setIsAdvance] = useState<boolean>(false);
  const [amount, setAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<string>("");
  const [method, setMethod] = useState<PaymentMethod>("BankTransfer");
  const [selectedAccountId, setSelectedAccountId] = useState<string>("");
  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [bankReference, setBankReference] = useState<string>("");
  const [supplierReference, setSupplierReference] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const createOutgoing = useCreateOutgoingPayment();
  const completeManual = useCompleteManualPayment();

  const isSubmitting = createOutgoing.isPending || completeManual.isPending;

  const resetForm = () => {
    setPartyId("");
    setIsAdvance(false);
    setAmount("");
    setCurrencyCode("");
    setMethod("BankTransfer");
    setSelectedAccountId("");
    setPaymentDate(new Date().toISOString().slice(0, 10));
    setBankReference("");
    setSupplierReference("");
    setErrorMsg("");
  };

  const handleClose = () => {
    resetForm();
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    if (!partyId) {
      setErrorMsg(t("errors.party_required"));
      return;
    }

    const numAmount = Number(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    if (!selectedAccountId) {
      setErrorMsg(t("errors.account_required"));
      return;
    }

    try {
      const type: PaymentType = isAdvance ? "SupplierAdvance" : "SupplierPayment";
      const request: CreatePaymentRequest = {
        legalEntityId,
        branchId: branchId || null,
        type,
        partyId,
        amount: Number(amount).toFixed(2),
        currencyCode: currencyCode as any,
        method,
        paymentAccountId: selectedAccountId,
        paymentDate,
        postingDate: paymentDate,
        bankReference: bankReference.trim() || null,
        supplierReference: supplierReference.trim() || null
      };

      const created = await createOutgoing.mutateAsync({
        legalEntityId,
        request
      });

      // Complete manual payment
      await completeManual.mutateAsync({
        legalEntityId,
        paymentId: created.id,
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

  const methodOptions = [
    { id: "BankTransfer", label: t("method.banktransfer") },
    { id: "Cash", label: t("method.cash") },
    { id: "Cheque", label: t("method.cheque") },
    { id: "Card", label: t("method.card") },
    { id: "PaymentGateway", label: t("method.paymentgateway") },
    { id: "Wallet", label: t("method.wallet") },
    { id: "Other", label: t("method.other") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={isAdvance ? t("pay.advanceTitle") : t("pay.title")}
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

        {/* Supplier Selection */}
        <PartyPicker
          label={t("pay.supplierLabel")}
          description={t("pay.supplierSearchHint")}
          value={partyId || undefined}
          onChange={(id) => setPartyId(id)}
          roleFilter="Supplier"
          enableQuickCreate={false}
          isRequired
        />

        {/* Advance Checkbox */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="pay-is-advance"
            checked={isAdvance}
            onChange={(e) => setIsAdvance(e.target.checked)}
            style={{ width: "16px", height: "16px", cursor: "pointer" }}
          />
          <label htmlFor="pay-is-advance" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 500 }}>
            {t("pay.isAdvanceCheckbox")}
          </label>
        </div>

        {/* Amount & Currency */}
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("pay.amountLabel")}
            value={amount}
            onChange={setAmount}
            placeholder="0.00"
            isRequired
            type="number"
          />
          <PaymentCurrencyPicker
            legalEntityId={legalEntityId}
            label={t("pay.currencyLabel")}
            value={currencyCode}
            onChange={setCurrencyCode}
            isRequired
          />
        </div>

        {/* Method & Source Account */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("pay.methodLabel")}
            selectedKey={method}
            onSelectionChange={(val) => setMethod(String(val ?? "") as PaymentMethod)}
            options={methodOptions}
            isRequired
          />

          <AccountPicker
            label={t("pay.sourceAccountLabel")}
            accounts={accounts}
            selectedId={selectedAccountId}
            onSelect={(acc) => {
              if (acc) {
                setSelectedAccountId(acc.id);
                if (acc.currencyCode) setCurrencyCode(acc.currencyCode);
              } else {
                setSelectedAccountId("");
                setCurrencyCode("");
              }
            }}
            filterType={method === "Cash" ? "Cashbox" : undefined}
            isRequired
          />
        </div>

        {/* Date & References */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("pay.dateLabel")}
            value={paymentDate}
            onChange={setPaymentDate}
            isRequired
          />
          <TextField
            label={t("pay.bankReferenceLabel")}
            value={bankReference}
            onChange={setBankReference}
            placeholder={t("pay.bankReferencePlaceholder")}
          />
        </div>

        <TextField
          label={t("pay.supplierReferenceLabel")}
          value={supplierReference}
          onChange={setSupplierReference}
          placeholder={t("pay.supplierReferencePlaceholder")}
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
          <Button variant="secondary" onPress={handleClose} isDisabled={isSubmitting}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting}>
            {isSubmitting ? t("pay.submitting") : t("pay.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
