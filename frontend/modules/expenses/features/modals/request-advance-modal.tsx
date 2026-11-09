import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useCreateAdvanceMutation,
  useCreateMyAdvanceMutation
} from "../../hooks/use-expenses-queries.ts";

export interface RequestAdvanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly employeeId?: string;
  readonly isSelfService?: boolean;
}

export function RequestAdvanceModal({
  isOpen,
  onClose,
  legalEntityId,
  employeeId,
  isSelfService = false
}: RequestAdvanceModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const createMutation = useCreateAdvanceMutation(legalEntityId);
  const createMyMutation = useCreateMyAdvanceMutation(legalEntityId);

  const [purpose, setPurpose] = useState("");
  const [amount, setAmount] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [dueDate, setDueDate] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (!purpose.trim()) {
      setError(t("modals.requestAdvance.purposeLabel"));
      return;
    }
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.requestAdvance.amountLabel"));
      return;
    }

    try {
      if (isSelfService) {
        await createMyMutation.mutateAsync({
          legalEntityId,
          employeeId: employeeId || "00000000-0000-0000-0000-000000000000",
          purpose: purpose.trim(),
          amount: parsedAmount,
          currencyCode,
          settlementDueDate: dueDate || null
        });
      } else {
        await createMutation.mutateAsync({
          legalEntityId,
          employeeId: employeeId || "00000000-0000-0000-0000-000000000000",
          purpose: purpose.trim(),
          amount: parsedAmount,
          currencyCode,
          settlementDueDate: dueDate || null
        });
      }

      onClose();
      setPurpose("");
      setAmount("");
      setDueDate("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  const isSubmitting = createMutation.isPending || createMyMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.requestAdvance.title")}
      description={t("modals.requestAdvance.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="request-advance-form" isDisabled={isSubmitting}>
            {isSubmitting ? t("common.loading") : t("modals.requestAdvance.submitButton")}
          </Button>
        </>
      }
    >
      <form id="request-advance-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.requestAdvance.purposeLabel")}
          value={purpose}
          onChange={setPurpose}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.requestAdvance.amountLabel")}
            type="number"
            value={amount}
            onChange={setAmount}
            isRequired
          />

          <TextField
            label={t("modals.requestAdvance.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val.toUpperCase())}
            isRequired
          />
        </div>

        <TextField
          label={t("modals.requestAdvance.dueDateLabel")}
          type="date"
          value={dueDate}
          onChange={setDueDate}
        />
      </form>
    </Dialog>
  );
}
