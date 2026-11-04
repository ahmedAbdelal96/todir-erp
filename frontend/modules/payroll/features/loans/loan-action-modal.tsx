import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useApplyLoanActionMutation } from "../../hooks/use-payroll-queries.ts";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { EmployeeReceivable, LoanActionType } from "../../types.ts";

export type ModalLoanAction =
  | "pause"
  | "resume"
  | "reschedule"
  | "repay"
  | "settle"
  | "writeOff";

export interface LoanActionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly receivable: EmployeeReceivable | null;
  readonly actionType: ModalLoanAction;
}

export function LoanActionModal({
  isOpen,
  onClose,
  legalEntityId,
  receivable,
  actionType
}: LoanActionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("payroll");

  const actionMutation = useApplyLoanActionMutation(legalEntityId, receivable?.id ?? "");

  const [reason, setReason] = useState("");
  const [repayAmount, setRepayAmount] = useState<number>(receivable?.outstandingPrincipal ?? 0);
  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [paymentReference, setPaymentReference] = useState("");
  const [newInstallmentCount, setNewInstallmentCount] = useState<number>(3);
  const [newInstallmentAmount, setNewInstallmentAmount] = useState<number>(
    receivable ? Math.round((receivable.outstandingPrincipal / 3) * 100) / 100 : 0
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!receivable) return null;

  const isPending = actionMutation.isPending;

  const getTitle = () => {
    switch (actionType) {
      case "pause":
        return t("loans.actions.pauseTitle");
      case "resume":
        return t("loans.actions.resumeTitle");
      case "reschedule":
        return t("loans.actions.rescheduleTitle");
      case "repay":
        return t("loans.actions.repayTitle");
      case "settle":
        return t("loans.actions.settleTitle");
      case "writeOff":
        return t("loans.actions.writeOffTitle");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    try {
      if (actionType === "pause") {
        await actionMutation.mutateAsync({
          action: "Pause" as LoanActionType,
          reason: reason.trim() || null
        });
      } else if (actionType === "resume") {
        await actionMutation.mutateAsync({
          action: "Resume" as LoanActionType,
          reason: reason.trim() || null
        });
      } else if (actionType === "reschedule") {
        await actionMutation.mutateAsync({
          action: "Reschedule" as LoanActionType,
          newInstallmentAmount,
          reason: reason.trim() || null
        });
      } else if (actionType === "repay") {
        if (repayAmount <= 0) {
          setErrorMessage(t("loans.actions.repayAmountPositiveRequired"));
          return;
        }
        await actionMutation.mutateAsync({
          action: "ExternalRepayment" as LoanActionType,
          amount: repayAmount,
          effectiveDate: paymentDate,
          externalReference: paymentReference.trim() || null,
          reason: reason.trim() || null
        });
      } else if (actionType === "settle") {
        await actionMutation.mutateAsync({
          action: "EarlySettlement" as LoanActionType,
          amount: receivable.outstandingPrincipal,
          reason: reason.trim() || null
        });
      } else if (actionType === "writeOff") {
        await actionMutation.mutateAsync({
          action: "WriteOff" as LoanActionType,
          amount: receivable.outstandingPrincipal,
          reason: reason.trim() || null
        });
      }

      onClose();
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          onClose();
        }
      }}
      title={getTitle()}
      description={t("loans.actions.receivableInfo", {
        type: receivable.type,
        balance: receivable.outstandingPrincipal.toLocaleString(),
        currency: receivable.currencyCode
      })}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={isPending}>
            {t("actions.cancel")}
          </Button>
          <Button
            variant={actionType === "writeOff" ? "danger" : "primary"}
            type="submit"
            form="loan-action-form"
            isDisabled={isPending}
          >
            {isPending ? t("actions.processing") : t("actions.confirm")}
          </Button>
        </>
      }
    >
      <form
        id="loan-action-form"
        onSubmit={handleSubmit}
        style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
      >
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {actionType === "reschedule" && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <TextField
              type="number"
              label={t("loans.actions.newInstallmentCount")}
              value={String(newInstallmentCount)}
              onChange={(val) => {
                const count = Number(val) || 1;
                setNewInstallmentCount(count);
                setNewInstallmentAmount(
                  Math.round((receivable.outstandingPrincipal / count) * 100) / 100
                );
              }}
              isRequired
            />

            <TextField
              type="number"
              label={t("loans.actions.newInstallmentAmount")}
              value={String(newInstallmentAmount)}
              onChange={(val) => setNewInstallmentAmount(Number(val) || 0)}
              isRequired
            />
          </div>
        )}

        {actionType === "repay" && (
          <>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
              <TextField
                type="number"
                label={t("loans.actions.repayAmount")}
                value={String(repayAmount)}
                onChange={(val) => setRepayAmount(Number(val) || 0)}
                isRequired
              />

              <LocalDateField
                label={t("loans.actions.paymentDate")}
                value={paymentDate}
                onChange={setPaymentDate}
                isRequired
              />
            </div>

            <TextField
              label={t("loans.actions.paymentReference")}
              value={paymentReference}
              onChange={setPaymentReference}
              placeholder={t("loans.actions.paymentReferencePlaceholder")}
            />
          </>
        )}

        <TextField
          label={t("loans.actions.reasonLabel")}
          value={reason}
          onChange={setReason}
          placeholder={t("loans.actions.reasonPlaceholder")}
          isRequired={actionType === "writeOff"}
        />
      </form>
    </Dialog>
  );
}
