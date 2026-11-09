import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useMatchCardMutation,
  useCreateExpenseFromCardMutation,
  useRecordCardAdjustmentMutation,
  useExpenseCategoriesQuery
} from "../../hooks/use-expenses-queries.ts";
import type { ExpenseCardTransactionDto } from "../../types.ts";

export interface MatchCardModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly cardTransaction: ExpenseCardTransactionDto | null;
}

export function MatchCardModal({
  isOpen,
  onClose,
  legalEntityId,
  cardTransaction
}: MatchCardModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const matchMutation = useMatchCardMutation(legalEntityId);

  const [expenseId, setExpenseId] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cardTransaction) return;
    setError(null);

    if (!expenseId.trim()) {
      setError(t("modals.matchCard.expenseIdLabel"));
      return;
    }

    try {
      await matchMutation.mutateAsync({
        cardId: cardTransaction.id,
        request: { expenseId: expenseId.trim() }
      });
      onClose();
      setExpenseId("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.matchCard.title")}
      description={t("modals.matchCard.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.matchCard.expenseIdLabel")}
          value={expenseId}
          onChange={(val) => setExpenseId(val)}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={matchMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={matchMutation.isPending}>
            {matchMutation.isPending ? t("common.loading") : t("modals.matchCard.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface CreateExpenseFromCardModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly cardTransaction: ExpenseCardTransactionDto | null;
}

export function CreateExpenseFromCardModal({
  isOpen,
  onClose,
  legalEntityId,
  cardTransaction
}: CreateExpenseFromCardModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const categoriesQuery = useExpenseCategoriesQuery(legalEntityId);
  const createFromCardMutation = useCreateExpenseFromCardMutation(legalEntityId);

  const [categoryId, setCategoryId] = useState("");
  const [businessPurpose, setBusinessPurpose] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const categories = categoriesQuery.data ?? [];
  const selectedCategory = categories.find((c) => c.id === categoryId) ?? categories[0];
  const activeVersionId = selectedCategory?.publishedVersionId ?? "";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cardTransaction) return;
    setError(null);

    if (!activeVersionId) {
      setError(t("modals.createExpenseFromCard.categoryLabel"));
      return;
    }

    try {
      await createFromCardMutation.mutateAsync({
        cardId: cardTransaction.id,
        request: {
          categoryVersionId: activeVersionId,
          businessPurpose: businessPurpose.trim() || null,
          description: description.trim() || null
        }
      });
      onClose();
      setBusinessPurpose("");
      setDescription("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.createExpenseFromCard.title")}
      description={t("modals.createExpenseFromCard.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <Select
          label={t("modals.createExpenseFromCard.categoryLabel")}
          selectedKey={categoryId || selectedCategory?.id || null}
          onSelectionChange={(key) => {
            if (key) setCategoryId(String(key));
          }}
          options={categories.map((cat) => ({
            id: cat.id,
            label: `${cat.name} (${cat.code})`
          }))}
          isRequired
        />

        <TextField
          label={t("modals.createExpenseFromCard.purposeLabel")}
          value={businessPurpose}
          onChange={(val) => setBusinessPurpose(val)}
          placeholder={t("modals.createExpenseFromCard.purposeLabel")}
        />

        <TextField
          label={t("modals.createExpenseFromCard.descriptionLabel")}
          value={description}
          onChange={(val) => setDescription(val)}
          placeholder={t("modals.createExpenseFromCard.descriptionLabel")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createFromCardMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createFromCardMutation.isPending}>
            {createFromCardMutation.isPending ? t("common.loading") : t("modals.createExpenseFromCard.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface RecordCardAdjustmentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly cardTransaction: ExpenseCardTransactionDto | null;
}

export function RecordCardAdjustmentModal({
  isOpen,
  onClose,
  legalEntityId,
  cardTransaction
}: RecordCardAdjustmentModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const adjustMutation = useRecordCardAdjustmentMutation(legalEntityId);

  const [kind, setKind] = useState("Credit");
  const [providerReference, setProviderReference] = useState("");
  const [amount, setAmount] = useState(() => (cardTransaction ? String(cardTransaction.amount) : ""));
  const [currencyCode, setCurrencyCode] = useState(() => (cardTransaction ? cardTransaction.currencyCode : "EGP"));
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (cardTransaction) {
      setAmount(String(cardTransaction.amount));
      setCurrencyCode(cardTransaction.currencyCode);
    }
  }, [cardTransaction]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cardTransaction) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.recordAdjustment.amountLabel"));
      return;
    }
    if (!reason.trim()) {
      setError(t("modals.recordAdjustment.reasonLabel"));
      return;
    }

    try {
      await adjustMutation.mutateAsync({
        cardId: cardTransaction.id,
        request: {
          kind,
          providerReference: providerReference.trim() || `ADJ-${Date.now()}`,
          amount: parsedAmount,
          currencyCode,
          reason: reason.trim()
        }
      });
      onClose();
      setReason("");
      setProviderReference("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.recordAdjustment.title")}
      description={t("modals.recordAdjustment.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <Select
          label={t("modals.recordAdjustment.kindLabel")}
          selectedKey={kind}
          onSelectionChange={(key) => {
            if (key) setKind(String(key));
          }}
          options={[
            { id: "Credit", label: "مردود ائتماني (Credit)" },
            { id: "Refund", label: "استرداد بنكي (Refund)" },
            { id: "Dispute", label: "نزاع بنكي (Dispute)" },
            { id: "Reversal", label: "عكس قيد (Reversal)" }
          ]}
        />

        <TextField
          label={t("modals.recordAdjustment.referenceLabel")}
          value={providerReference}
          onChange={(val) => setProviderReference(val)}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.recordAdjustment.amountLabel")}
            type="number"
            value={amount}
            onChange={(val) => setAmount(val)}
            isRequired
          />
          <TextField
            label={t("modals.recordAdjustment.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val.toUpperCase())}
            isRequired
          />
        </div>

        <TextField
          label={t("modals.recordAdjustment.reasonLabel")}
          value={reason}
          onChange={(val) => setReason(val)}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={adjustMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={adjustMutation.isPending}>
            {adjustMutation.isPending ? t("common.loading") : t("modals.recordAdjustment.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
