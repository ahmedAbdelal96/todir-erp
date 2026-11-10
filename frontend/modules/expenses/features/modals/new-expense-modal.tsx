import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useOptionalLegalEntityContext } from "../../../../core/runtime/runtime-provider.tsx";
import {
  useExpenseCategoriesQuery,
  useCreateExpenseMutation,
  useCreateMyExpenseMutation,
  useExpenseRatePoliciesQuery
} from "../../hooks/use-expenses-queries.ts";
import { expensesApi } from "../../services/expenses-api.ts";
import type { PaymentSource, ExpenseDto } from "../../types.ts";

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
  const legalEntityContext = useOptionalLegalEntityContext();
  const defaultCurrency = legalEntityContext?.baseCurrencyCode || "EGP";

  const categoriesQuery = useExpenseCategoriesQuery(legalEntityId);
  const mileagePoliciesQuery = useExpenseRatePoliciesQuery(legalEntityId, { kind: "Mileage", status: "Published" });
  const perDiemPoliciesQuery = useExpenseRatePoliciesQuery(legalEntityId, { kind: "PerDiem", status: "Published" });
  const createMutation = useCreateExpenseMutation(legalEntityId);
  const createMyMutation = useCreateMyExpenseMutation(legalEntityId);

  const publishedMileagePolicy = mileagePoliciesQuery.data?.items?.[0];
  const publishedPerDiemPolicy = perDiemPoliciesQuery.data?.items?.[0];

  const [expenseMode, setExpenseMode] = useState<"general" | "mileage" | "perDiem">("general");
  const [merchantName, setMerchantName] = useState("");
  const [expenseDate, setExpenseDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState(defaultCurrency);
  const [paymentSource, setPaymentSource] = useState<PaymentSource>("EmployeePaid");
  const [categoryId, setCategoryId] = useState("");
  const [businessPurpose, setBusinessPurpose] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  // Mileage-specific fields
  const [distanceKm, setDistanceKm] = useState<string>("");
  const [ratePerKm, setRatePerKm] = useState<string>("5.00");
  const [travelRoute, setTravelRoute] = useState<string>("");

  // Per-diem-specific fields
  const [daysCount, setDaysCount] = useState<string>("");
  const [dailyRate, setDailyRate] = useState<string>("500.00");
  const [destinationCity, setDestinationCity] = useState<string>("");

  React.useEffect(() => {
    if (publishedMileagePolicy?.rate) {
      setRatePerKm(String(publishedMileagePolicy.rate));
    }
  }, [publishedMileagePolicy?.rate]);

  React.useEffect(() => {
    if (publishedPerDiemPolicy?.rate) {
      setDailyRate(String(publishedPerDiemPolicy.rate));
    }
  }, [publishedPerDiemPolicy?.rate]);

  const categories = categoriesQuery.data ?? [];
  const selectedCategory = categories.find((c) => c.id === categoryId) ?? categories[0];
  const activeVersionId = selectedCategory?.publishedVersionId ?? "";

  const handleDistanceChange = (kmVal: string, manualRate?: string) => {
    setDistanceKm(kmVal);
    const km = parseFloat(kmVal);
    const rate = manualRate !== undefined ? parseFloat(manualRate) : (publishedMileagePolicy ? Number(publishedMileagePolicy.rate) : parseFloat(ratePerKm));
    if (!isNaN(km) && !isNaN(rate) && km > 0 && rate > 0) {
      setAmount((km * rate).toFixed(2));
    }
  };

  const handleDaysChange = (daysVal: string, manualRate?: string) => {
    setDaysCount(daysVal);
    const days = parseFloat(daysVal);
    const rate = manualRate !== undefined ? parseFloat(manualRate) : (publishedPerDiemPolicy ? Number(publishedPerDiemPolicy.rate) : parseFloat(dailyRate));
    if (!isNaN(days) && !isNaN(rate) && days > 0 && rate > 0) {
      setAmount((days * rate).toFixed(2));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const parsedAmount = parseFloat(amount);
    const finalMerchant =
      merchantName.trim() ||
      (expenseMode === "mileage"
        ? `${t("mileagePerDiem.modeMileage")}: ${travelRoute || distanceKm + " km"}`
        : expenseMode === "perDiem"
        ? `${t("mileagePerDiem.modePerDiem")}: ${destinationCity || daysCount + " days"}`
        : "");

    if (!finalMerchant) {
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

    const finalPurpose =
      expenseMode === "mileage"
        ? `${businessPurpose.trim()} [${t("mileagePerDiem.distanceKm")}: ${distanceKm} km, ${travelRoute}]`.trim()
        : expenseMode === "perDiem"
        ? `${businessPurpose.trim()} [${t("mileagePerDiem.daysCount")}: ${daysCount}, ${destinationCity}]`.trim()
        : businessPurpose.trim() || null;

    try {
      let createdExpense: ExpenseDto;
      if (isSelfService) {
        createdExpense = await createMyMutation.mutateAsync({
          legalEntityId,
          merchantName: finalMerchant,
          expenseDate,
          amount: parsedAmount,
          currencyCode,
          paymentSource,
          categoryVersionId: activeVersionId,
          businessPurpose: finalPurpose,
          description: description.trim() || null
        });
      } else {
        createdExpense = await createMutation.mutateAsync({
          legalEntityId,
          merchantName: finalMerchant,
          expenseDate,
          amount: parsedAmount,
          currencyCode,
          paymentSource,
          categoryVersionId: activeVersionId,
          businessPurpose: finalPurpose,
          description: description.trim() || null
        });
      }

      if (expenseMode === "mileage" && distanceKm) {
        const km = parseFloat(distanceKm);
        const rate = publishedMileagePolicy ? Number(publishedMileagePolicy.rate) : parseFloat(ratePerKm) || 0;
        await expensesApi.setMileage(legalEntityId, createdExpense.id, {
          distance: km,
          rate,
          ratePolicyVersionId: publishedMileagePolicy?.id || null,
          vehicleType: publishedMileagePolicy?.vehicleType || null,
          route: travelRoute.trim() || null,
          distanceUnit: "KM"
        });
      } else if (expenseMode === "perDiem" && daysCount) {
        const days = parseFloat(daysCount);
        const rate = publishedPerDiemPolicy ? Number(publishedPerDiemPolicy.rate) : parseFloat(dailyRate) || 0;
        await expensesApi.setPerDiem(legalEntityId, createdExpense.id, {
          days,
          dailyRate: rate,
          ratePolicyVersionId: publishedPerDiemPolicy?.id || null,
          destination: destinationCity.trim() || publishedPerDiemPolicy?.locationCode || null,
          providedMeals: 0
        });
      }

      onClose();
      setMerchantName("");
      setAmount("");
      setBusinessPurpose("");
      setDescription("");
      setDistanceKm("");
      setTravelRoute("");
      setDaysCount("");
      setDestinationCity("");
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

        {/* Expense Mode Selector */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid #cbd5e1", paddingBottom: "0.5rem" }}>
          <button
            type="button"
            onClick={() => setExpenseMode("general")}
            style={{
              padding: "0.375rem 0.75rem",
              borderRadius: "0.375rem",
              border: "none",
              backgroundColor: expenseMode === "general" ? "#102a43" : "#f1f5f9",
              color: expenseMode === "general" ? "#ffffff" : "#334155",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer"
            }}
          >
            {t("mileagePerDiem.modeGeneral")}
          </button>
          <button
            type="button"
            onClick={() => setExpenseMode("mileage")}
            style={{
              padding: "0.375rem 0.75rem",
              borderRadius: "0.375rem",
              border: "none",
              backgroundColor: expenseMode === "mileage" ? "#102a43" : "#f1f5f9",
              color: expenseMode === "mileage" ? "#ffffff" : "#334155",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer"
            }}
          >
            {t("mileagePerDiem.modeMileage")}
          </button>
          <button
            type="button"
            onClick={() => setExpenseMode("perDiem")}
            style={{
              padding: "0.375rem 0.75rem",
              borderRadius: "0.375rem",
              border: "none",
              backgroundColor: expenseMode === "perDiem" ? "#102a43" : "#f1f5f9",
              color: expenseMode === "perDiem" ? "#ffffff" : "#334155",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer"
            }}
          >
            {t("mileagePerDiem.modePerDiem")}
          </button>
        </div>

        {expenseMode === "general" && (
          <TextField
            label={t("modals.newExpense.merchantLabel")}
            value={merchantName}
            onChange={setMerchantName}
            isRequired
          />
        )}

        {expenseMode === "mileage" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "0.375rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
              <TextField
                label={t("mileagePerDiem.distanceKm")}
                type="number"
                value={distanceKm}
                onChange={(val) => handleDistanceChange(val, ratePerKm)}
                isRequired
              />
              <TextField
                label={t("mileagePerDiem.ratePerKm")}
                type="number"
                value={ratePerKm}
                onChange={(val) => {
                  setRatePerKm(val);
                  handleDistanceChange(distanceKm, val);
                }}
                isRequired
              />
            </div>
            <TextField
              label={t("common.description")}
              placeholder={t("mileagePerDiem.routePlaceholder")}
              value={travelRoute}
              onChange={setTravelRoute}
            />
          </div>
        )}

        {expenseMode === "perDiem" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "0.375rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
              <TextField
                label={t("mileagePerDiem.daysCount")}
                type="number"
                value={daysCount}
                onChange={(val) => handleDaysChange(val, dailyRate)}
                isRequired
              />
              <TextField
                label={t("mileagePerDiem.dailyRate")}
                type="number"
                value={dailyRate}
                onChange={(val) => {
                  setDailyRate(val);
                  handleDaysChange(daysCount, val);
                }}
                isRequired
              />
            </div>
            <TextField
              label={t("modals.newRatePolicy.locationCodeLabel")}
              placeholder={t("mileagePerDiem.locationPlaceholder")}
              value={destinationCity}
              onChange={setDestinationCity}
            />
          </div>
        )}

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