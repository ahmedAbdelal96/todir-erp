import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useIssueAdvanceMutation } from "../../hooks/use-expenses-queries.ts";
import type { BusinessAdvanceDto } from "../../types.ts";

export interface IssueAdvanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly advance: BusinessAdvanceDto | null;
}

export function IssueAdvanceModal({
  isOpen,
  onClose,
  legalEntityId,
  advance
}: IssueAdvanceModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const issueMutation = useIssueAdvanceMutation(legalEntityId);

  const [accountId, setAccountId] = useState("00000000-0000-0000-0000-000000000001");
  const [method, setMethod] = useState("BankTransfer");
  const [amount, setAmount] = useState(() => (advance ? String(advance.amount) : ""));
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [postingDate, setPostingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (advance) setAmount(String(advance.amount));
  }, [advance]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!advance) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.issueAdvance.amountLabel"));
      return;
    }

    try {
      await issueMutation.mutateAsync({
        id: advance.id,
        request: {
          paymentAccountId: accountId,
          method,
          amount: parsedAmount,
          paymentDate,
          postingDate,
          version: advance.version
        }
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  const methodOptions: readonly SelectOption[] = [
    { id: "BankTransfer", label: t("modals.issueAdvance.methods.bankTransfer") },
    { id: "Cash", label: t("modals.issueAdvance.methods.cash") },
    { id: "Cheque", label: t("modals.issueAdvance.methods.cheque") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.issueAdvance.title")}
      description={t("modals.issueAdvance.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={issueMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="issue-advance-form" isDisabled={issueMutation.isPending}>
            {issueMutation.isPending ? t("common.loading") : t("modals.issueAdvance.submitButton")}
          </Button>
        </>
      }
    >
      <form id="issue-advance-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              backgroundColor: "var(--color-surface-danger, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              borderRadius: "var(--radius-md, 0.5rem)",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("modals.issueAdvance.accountLabel")}
          value={accountId}
          onChange={setAccountId}
          isRequired
        />

        <Select
          label={t("modals.issueAdvance.methodLabel")}
          selectedKey={method}
          onSelectionChange={(key) => setMethod(String(key))}
          options={methodOptions}
        />

        <TextField
          label={t("modals.issueAdvance.amountLabel")}
          type="number"
          value={amount}
          onChange={setAmount}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.issueAdvance.paymentDateLabel")}
            type="date"
            value={paymentDate}
            onChange={setPaymentDate}
            isRequired
          />
          <TextField
            label={t("modals.issueAdvance.postingDateLabel")}
            type="date"
            value={postingDate}
            onChange={setPostingDate}
            isRequired
          />
        </div>
      </form>
    </Dialog>
  );
}
