import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { useApplyPayment } from "../hooks/payment-queries.ts";
import { useOpenItemsQuery, type PartyOpenItem } from "../../subledger/public.ts";
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
  const [amount, setAmount] = useState<string>("");
  const [applicationDate, setApplicationDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [errorMsg, setErrorMsg] = useState<string>("");

  const applyMutation = useApplyPayment();

  const openItemsQuery = useOpenItemsQuery(
    {
      legalEntityId,
      ...(payment?.partyId ? { partyId: payment.partyId } : {}),
      openOnly: true
    },
    Boolean(isOpen && legalEntityId && payment)
  );
  const openItems = openItemsQuery.data?.items || [];
  const selectedItem = openItems.find((i) => i.id === targetOpenItemId);

  const paymentTotal = payment ? Number(payment.amount) || 0 : 0;
  const numAmount = Number(amount) || 0;
  const residualCredit = Math.max(0, paymentTotal - numAmount);

  if (!payment) return <></>;

  const handleClose = () => {
    setErrorMsg("");
    setTargetOpenItemId("");
    setAmount("");
    onOpenChange(false);
  };

  const handleSelectItem = (item: PartyOpenItem) => {
    setTargetOpenItemId(item.id);
    const itemRemaining = Number(item.openTransactionAmount) || 0;
    const safeDefault = Math.min(paymentTotal, itemRemaining);
    setAmount(safeDefault > 0 ? safeDefault.toFixed(2) : "0.00");
    setErrorMsg("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    if (!targetOpenItemId.trim()) {
      setErrorMsg(t("apply.targetItemRequired"));
      return;
    }

    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    if (selectedItem) {
      const itemRemaining = Number(selectedItem.openTransactionAmount) || 0;
      if (numAmount > itemRemaining) {
        setErrorMsg(t("apply.amountExceedsOpenItem"));
        return;
      }
    }

    if (numAmount > paymentTotal) {
      setErrorMsg(t("apply.amountExceedsAvailableCredit"));
      return;
    }

    try {
      const request: ApplyPaymentRequest = {
        applicationDate,
        settlementDate: applicationDate,
        lines: [
          {
            targetOpenItemId: targetOpenItemId.trim(),
            amount: numAmount.toFixed(2)
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

        {/* Open Items Picker */}
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.5rem", color: "#102a43" }}>
            {t("apply.selectOpenItemLabel")} <span style={{ color: "#ef4444" }}>*</span>
          </label>

          {openItemsQuery.isLoading ? (
            <div style={{ padding: "1rem", textAlign: "center", color: "#64748b", fontSize: "0.875rem" }}>
              {t("list.loading")}
            </div>
          ) : openItems.length === 0 ? (
            <div
              style={{
                padding: "1rem",
                borderRadius: "6px",
                border: "1px dashed #cbd5e1",
                backgroundColor: "#f8fafc",
                textAlign: "center",
                color: "#64748b",
                fontSize: "0.875rem"
              }}
            >
              {t("apply.noOpenItems")}
            </div>
          ) : (
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem",
                maxHeight: "220px",
                overflowY: "auto",
                border: "1px solid #cbd5e1",
                borderRadius: "6px",
                padding: "0.5rem"
              }}
              role="radiogroup"
              aria-label={t("apply.selectOpenItemLabel")}
            >
              {openItems.map((item) => {
                const isSelected = targetOpenItemId === item.id;
                const docNo = item.sourceDisplayNumber || item.sourceReference || item.id;
                return (
                  <div
                    key={item.id}
                    onClick={() => handleSelectItem(item)}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "0.75rem",
                      borderRadius: "6px",
                      cursor: "pointer",
                      border: isSelected ? "2px solid #2563eb" : "1px solid #e2e8f0",
                      backgroundColor: isSelected ? "#eff6ff" : "#ffffff",
                      transition: "all 0.15s ease"
                    }}
                    role="radio"
                    aria-checked={isSelected}
                    tabIndex={0}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                      <input
                        type="radio"
                        name="openItemSelection"
                        checked={isSelected}
                        onChange={() => handleSelectItem(item)}
                        aria-label={docNo}
                      />
                      <div>
                        <div style={{ fontWeight: 700, color: "#102a43", fontSize: "0.875rem" }}>
                          <bdi>{docNo}</bdi>
                        </div>
                        <div style={{ display: "flex", gap: "1rem", fontSize: "0.75rem", color: "#64748b", marginTop: "0.125rem" }}>
                          <span>
                            {t("apply.documentDate")}: <LocalDateDisplay value={item.documentDate} />
                          </span>
                          <span>
                            {t("apply.dueDate")}: <LocalDateDisplay value={item.dueDate} />
                          </span>
                        </div>
                      </div>
                    </div>

                    <div style={{ textAlign: "end" }}>
                      <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("apply.openAmount")}:</div>
                      <div style={{ fontWeight: 700, color: "#047857", fontSize: "0.875rem" }}>
                        <MoneyDisplay value={{ amount: item.openTransactionAmount, currency: (item.transactionCurrency || payment.currencyCode) as any }} />
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Safe Allocation Calculation & Residual Credit Box */}
        {selectedItem && (
          <div
            style={{
              backgroundColor: "#f0fdf4",
              border: "1px solid #bbf7d0",
              borderRadius: "6px",
              padding: "0.875rem 1rem",
              fontSize: "0.875rem",
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
              gap: "0.75rem"
            }}
          >
            <div>
              <span style={{ color: "#166534", display: "block", fontSize: "0.75rem" }}>
                {t("apply.allocatedAmountLabel")}:
              </span>
              <span style={{ fontWeight: 800, fontSize: "1.125rem", color: "#166534" }}>
                <MoneyDisplay value={{ amount: String(numAmount.toFixed(2)), currency: payment.currencyCode }} />
              </span>
            </div>

            <div>
              <span style={{ color: "#065f46", display: "block", fontSize: "0.75rem" }}>
                {t("apply.residualCreditLabel")}:
              </span>
              <span style={{ fontWeight: 800, fontSize: "1.125rem", color: "#065f46" }}>
                <MoneyDisplay value={{ amount: String(residualCredit.toFixed(2)), currency: payment.currencyCode }} />
              </span>
            </div>
          </div>
        )}

        <div style={{ fontSize: "0.75rem", color: "#64748b" }}>
          {t("apply.singleItemNotice")}
        </div>

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
        <LocalDateField
          label={t("apply.dateLabel")}
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
          <Button variant="primary" type="submit" isDisabled={applyMutation.isPending || !targetOpenItemId}>
            {applyMutation.isPending ? t("apply.submitting") : t("apply.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
