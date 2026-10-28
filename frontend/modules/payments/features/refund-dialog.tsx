import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { AccountPicker } from "../components/account-picker.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { useCreateRefund } from "../hooks/payment-queries.ts";
import type { PaymentRecord, PaymentAccount, PaymentMethod, CreateRefundRequest } from "../types.ts";

export interface RefundDialogProps {
  readonly payment: PaymentRecord | null;
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly accounts: readonly PaymentAccount[];
  readonly onSuccess?: () => void;
}

export function RefundDialog({
  payment,
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  accounts,
  onSuccess
}: RefundDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [amount, setAmount] = useState<string>(payment?.amount || "");
  const [method, setMethod] = useState<PaymentMethod>(payment?.method || "BankTransfer");
  const [selectedAccountId, setSelectedAccountId] = useState<string>(payment?.paymentAccountId || "");
  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [reasonCode, setReasonCode] = useState<string>("customer-requested");
  const [bankReference, setBankReference] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const createRefund = useCreateRefund();

  if (!payment) return <></>;

  const handleClose = () => {
    setErrorMsg("");
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    const numAmount = Number(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    if (numAmount > Number(payment.amount)) {
      setErrorMsg(t("errors.refund_exceeds_available"));
      return;
    }

    if (!selectedAccountId) {
      setErrorMsg(t("errors.account_required"));
      return;
    }

    try {
      const request: CreateRefundRequest = {
        paymentAccountId: selectedAccountId,
        method,
        amount: Number(amount).toFixed(2),
        paymentDate,
        postingDate: paymentDate,
        reasonCode,
        bankReference: bankReference.trim() || null
      };

      await createRefund.mutateAsync({
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

  const reasonOptions = [
    { id: "customer-requested", label: t("refund.reasonCustomerRequested") },
    { id: "duplicate-payment", label: t("refund.reasonDuplicatePayment") },
    { id: "order-cancelled", label: t("refund.reasonOrderCancelled") },
    { id: "goods-returned", label: t("refund.reasonGoodsReturned") },
    { id: "other", label: t("refund.reasonOther") }
  ];

  const methodOptions = [
    { id: "BankTransfer", label: t("method.banktransfer") },
    { id: "Cash", label: t("method.cash") },
    { id: "Card", label: t("method.card") },
    { id: "PaymentGateway", label: t("method.paymentgateway") },
    { id: "Other", label: t("method.other") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={t("refund.title")}
      size="medium"
    >
      <form noValidate onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        {/* Original Payment Context */}
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
            <span style={{ color: "#64748b" }}>{t("refund.originalPayment")}:</span>
            <span style={{ fontWeight: 700 }}><bdi>{payment.number}</bdi></span>
          </div>
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <span style={{ color: "#64748b" }}>{t("refund.originalAmount")}:</span>
            <span style={{ fontWeight: 700 }}>
              <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
            </span>
          </div>
        </div>

        {/* Refund Amount */}
        <TextField
          label={t("refund.amountLabel")}
          value={amount}
          onChange={setAmount}
          placeholder="0.00"
          isRequired
          type="number"
        />

        {/* Reason Code */}
        <Select
          label={t("refund.reasonLabel")}
          selectedKey={reasonCode}
          onSelectionChange={(val) => setReasonCode(String(val ?? ""))}
          options={reasonOptions}
          isRequired
        />

        {/* Method & Account */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("refund.methodLabel")}
            selectedKey={method}
            onSelectionChange={(val) => setMethod(String(val ?? "") as PaymentMethod)}
            options={methodOptions}
            isRequired
          />

          <AccountPicker
            label={t("refund.accountLabel")}
            accounts={accounts}
            selectedId={selectedAccountId}
            onSelect={(acc) => setSelectedAccountId(acc?.id || "")}
            isRequired
          />
        </div>

        {/* Date & Reference */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("refund.dateLabel")}
            value={paymentDate}
            onChange={setPaymentDate}
            isRequired
          />
          <TextField
            label={t("refund.referenceLabel")}
            value={bankReference}
            onChange={setBankReference}
            placeholder={t("refund.referencePlaceholder")}
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
          <Button variant="secondary" onPress={handleClose} isDisabled={createRefund.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="danger" type="submit" isDisabled={createRefund.isPending}>
            {createRefund.isPending ? t("refund.submitting") : t("refund.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
