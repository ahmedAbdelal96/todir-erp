import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useExpenseCategoriesQuery,
  useCreateExpenseMutation,
  useCreateMyExpenseMutation
} from "../../hooks/use-expenses-queries.ts";
import type { PaymentSource } from "../../types.ts";

export interface NewExpenseModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly isSelfService?: boolean;
}

export function NewExpenseModal({
  isOpen,
  onClose,
  legalEntityId,
  isSelfService = false
}: NewExpenseModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const categoriesQuery = useExpenseCategoriesQuery(legalEntityId);
  const createMutation = useCreateExpenseMutation(legalEntityId);
  const createMyMutation = useCreateMyExpenseMutation(legalEntityId);

  const [merchantName, setMerchantName] = useState("");
  const [expenseDate, setExpenseDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [paymentSource, setPaymentSource] = useState<PaymentSource>("EmployeePaid");
  const [categoryId, setCategoryId] = useState("");
  const [businessPurpose, setBusinessPurpose] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const categories = categoriesQuery.data ?? [];
  const selectedCategory = categories.find((c) => c.id === categoryId) ?? categories[0];
  const activeVersionId = selectedCategory?.publishedVersionId ?? "";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (!merchantName.trim()) {
      setError(t("modals.newExpense.merchantLabel"));
      return;
    }
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.newExpense.amountLabel"));
      return;
    }
    if (!activeVersionId) {
      setError(t("modals.newExpense.categoryLabel"));
      return;
    }

    try {
      if (isSelfService) {
        await createMyMutation.mutateAsync({
          legalEntityId,
          merchantName: merchantName.trim(),
          expenseDate,
          amount: parsedAmount,
          currencyCode,
          paymentSource,
          categoryVersionId: activeVersionId,
          businessPurpose: businessPurpose.trim() || null,
          description: description.trim() || null
        });
      } else {
        await createMutation.mutateAsync({
          legalEntityId,
          merchantName: merchantName.trim(),
          expenseDate,
          amount: parsedAmount,
          currencyCode,
          paymentSource,
          categoryVersionId: activeVersionId,
          businessPurpose: businessPurpose.trim() || null,
          description: description.trim() || null
        });
      }

      onClose();
      setMerchantName("");
      setAmount("");
      setBusinessPurpose("");
      setDescription("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  const isSubmitting = createMutation.isPending || createMyMutation.isPending;

  const sourceOptions: readonly SelectOption[] = [
    { id: "EmployeePaid", label: t("states.source.EmployeePaid") },
    { id: "CompanyCard", label: t("states.source.CompanyCard") },
    { id: "PettyCash", label: t("states.source.PettyCash") },
    { id: "Advance", label: t("states.source.Advance") }
  ];

  const categoryOptions: readonly SelectOption[] = categories.map((cat) => ({
    id: cat.id,
    label: `${cat.name} (${cat.code})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("modals.newExpense.title")}
      description={t("modals.newExpense.description")}
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" form="new-expense-form" isDisabled={isSubmitting}>
            {isSubmitting ? t("common.loading") : t("modals.newExpense.submitButton")}
          </Button>
        </>
      }
    >
      <form id="new-expense-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
          label={t("modals.newExpense.merchantLabel")}
          value={merchantName}
          onChange={setMerchantName}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.newExpense.amountLabel")}
            type="number"
            value={amount}
            onChange={setAmount}
            isRequired
          />

          <TextField
            label={t("modals.newExpense.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val.toUpperCase())}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.newExpense.dateLabel")}
            type="date"
            value={expenseDate}
            onChange={setExpenseDate}
            isRequired
          />

          <Select
            label={t("modals.newExpense.sourceLabel")}
            selectedKey={paymentSource}
            onSelectionChange={(key) => setPaymentSource(String(key) as PaymentSource)}
            options={sourceOptions}
          />
        </div>

        <Select
          label={t("modals.newExpense.categoryLabel")}
          selectedKey={categoryId || selectedCategory?.id || null}
          onSelectionChange={(key) => setCategoryId(String(key))}
          options={categoryOptions}
          isRequired
        />

        <TextField
          label={t("modals.newExpense.purposeLabel")}
          value={businessPurpose}
          onChange={setBusinessPurpose}
          placeholder={t("modals.newExpense.purposeLabel")}
        />

        <TextField
          label={t("modals.newExpense.descriptionLabel")}
          value={description}
          onChange={setDescription}
          placeholder={t("modals.newExpense.descriptionLabel")}
        />
      </form>
    </Dialog>
  );
}
