import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useApplyAdvanceMutation,
  useReturnAdvanceMutation,
  useRequestAdvanceWriteOffMutation
} from "../../hooks/use-expenses-queries.ts";
import type { BusinessAdvanceDto } from "../../types.ts";

export interface ApplyAdvanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly advance: BusinessAdvanceDto | null;
}

export function ApplyAdvanceModal({
  isOpen,
  onClose,
  legalEntityId,
  advance
}: ApplyAdvanceModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const applyMutation = useApplyAdvanceMutation(legalEntityId);

  const [expenseId, setExpenseId] = useState("");
  const [amount, setAmount] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!advance) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (!expenseId.trim()) {
      setError(t("modals.applyAdvance.expenseLabel"));
      return;
    }
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.applyAdvance.amountLabel"));
      return;
    }

    try {
      await applyMutation.mutateAsync({
        id: advance.id,
        request: {
          expenseId: expenseId.trim(),
          amount: parsedAmount
        }
      });
      onClose();
      setExpenseId("");
      setAmount("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.applyAdvance.title")}
      description={t("modals.applyAdvance.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={applyMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="apply-advance-form" isDisabled={applyMutation.isPending}>
            {applyMutation.isPending ? t("common.loading") : t("modals.applyAdvance.submitButton")}
          </Button>
        </>
      }
    >
      <form id="apply-advance-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.applyAdvance.expenseLabel")}
          value={expenseId}
          onChange={setExpenseId}
          isRequired
        />

        <TextField
          label={t("modals.applyAdvance.amountLabel")}
          type="number"
          value={amount}
          onChange={setAmount}
          isRequired
        />
      </form>
    </Dialog>
  );
}

export interface ReturnAdvanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly advance: BusinessAdvanceDto | null;
}

export function ReturnAdvanceModal({
  isOpen,
  onClose,
  legalEntityId,
  advance
}: ReturnAdvanceModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const returnMutation = useReturnAdvanceMutation(legalEntityId);

  const [amount, setAmount] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!advance) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.returnAdvance.amountLabel"));
      return;
    }

    try {
      await returnMutation.mutateAsync({
        id: advance.id,
        request: {
          amount: parsedAmount,
          version: advance.version
        }
      });
      onClose();
      setAmount("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.returnAdvance.title")}
      description={t("modals.returnAdvance.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={returnMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="return-advance-form" isDisabled={returnMutation.isPending}>
            {returnMutation.isPending ? t("common.loading") : t("modals.returnAdvance.submitButton")}
          </Button>
        </>
      }
    >
      <form id="return-advance-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.returnAdvance.amountLabel")}
          type="number"
          value={amount}
          onChange={setAmount}
          isRequired
        />
      </form>
    </Dialog>
  );
}

export interface WriteOffAdvanceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly advance: BusinessAdvanceDto | null;
}

export function WriteOffAdvanceModal({
  isOpen,
  onClose,
  legalEntityId,
  advance
}: WriteOffAdvanceModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const writeOffMutation = useRequestAdvanceWriteOffMutation(legalEntityId);

  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!advance) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.writeOffAdvance.amountLabel"));
      return;
    }
    if (!reason.trim()) {
      setError(t("modals.writeOffAdvance.reasonLabel"));
      return;
    }

    try {
      await writeOffMutation.mutateAsync({
        id: advance.id,
        request: {
          amount: parsedAmount,
          reason: reason.trim(),
          version: advance.version
        }
      });
      onClose();
      setAmount("");
      setReason("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.writeOffAdvance.title")}
      description={t("modals.writeOffAdvance.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={writeOffMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="write-off-advance-form" isDisabled={writeOffMutation.isPending}>
            {writeOffMutation.isPending ? t("common.loading") : t("modals.writeOffAdvance.submitButton")}
          </Button>
        </>
      }
    >
      <form id="write-off-advance-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.writeOffAdvance.amountLabel")}
          type="number"
          value={amount}
          onChange={setAmount}
          isRequired
        />

        <TextField
          label={t("modals.writeOffAdvance.reasonLabel")}
          value={reason}
          onChange={setReason}
          isRequired
        />
      </form>
    </Dialog>
  );
}
