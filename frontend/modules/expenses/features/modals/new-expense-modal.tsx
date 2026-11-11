import React, { useState, useMemo, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select, type SelectOption } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useOptionalLegalEntityContext } from "../../../../core/runtime/runtime-provider.tsx";
import {
  useExpenseCategoriesQuery,
  useMyExpenseCategoriesQuery,
  useCreateExpenseMutation,
  useCreateMyExpenseMutation,
  useExpenseRatePoliciesQuery,
  useMyExpenseRatePoliciesQuery
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

  const categoriesQuery = useExpenseCategoriesQuery(legalEntityId, !isSelfService);
  const myCategoriesQuery = useMyExpenseCategoriesQuery(legalEntityId, isSelfService);
  const mileagePoliciesQuery = useExpenseRatePoliciesQuery(legalEntityId, { kind: "Mileage", status: "Published" }, !isSelfService);
  const myMileagePoliciesQuery = useMyExpenseRatePoliciesQuery(legalEntityId, { kind: "Mileage" }, isSelfService);
  const perDiemPoliciesQuery = useExpenseRatePoliciesQuery(legalEntityId, { kind: "PerDiem", status: "Published" }, !isSelfService);
  const myPerDiemPoliciesQuery = useMyExpenseRatePoliciesQuery(legalEntityId, { kind: "PerDiem" }, isSelfService);
  const createMutation = useCreateExpenseMutation(legalEntityId);
  const createMyMutation = useCreateMyExpenseMutation(legalEntityId);

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
  const [travelRoute, setTravelRoute] = useState<string>("");
  const [roundTrip, setRoundTrip] = useState(false);
  const [selectedMileagePolicyId, setSelectedMileagePolicyId] = useState<string>("");

  // Per-diem-specific fields
  const [daysCount, setDaysCount] = useState<string>("");
  const [destinationCity, setDestinationCity] = useState<string>("");
  const [providedMeals, setProvidedMeals] = useState<number>(0);
  const [selectedPerDiemPolicyId, setSelectedPerDiemPolicyId] = useState<string>("");

  // Filter effective published rate policies based on date
  const effectiveMileagePolicies = useMemo(() => {
    const list = (isSelfService ? myMileagePoliciesQuery.data?.items : mileagePoliciesQuery.data?.items) ?? [];
    return list.filter((p) => p.effectiveFrom <= expenseDate && (!p.effectiveTo || p.effectiveTo >= expenseDate));
  }, [isSelfService, mileagePoliciesQuery.data?.items, myMileagePoliciesQuery.data?.items, expenseDate]);

  const selectedMileagePolicy = useMemo(() => {
    if (effectiveMileagePolicies.length === 0) return null;
    return effectiveMileagePolicies.find((p) => p.id === selectedMileagePolicyId) ?? effectiveMileagePolicies[0];
  }, [effectiveMileagePolicies, selectedMileagePolicyId]);

  const effectivePerDiemPolicies = useMemo(() => {
    const list = (isSelfService ? myPerDiemPoliciesQuery.data?.items : perDiemPoliciesQuery.data?.items) ?? [];
    return list.filter((p) => p.effectiveFrom <= expenseDate && (!p.effectiveTo || p.effectiveTo >= expenseDate));
  }, [isSelfService, perDiemPoliciesQuery.data?.items, myPerDiemPoliciesQuery.data?.items, expenseDate]);

  const selectedPerDiemPolicy = useMemo(() => {
    if (effectivePerDiemPolicies.length === 0) return null;
    return effectivePerDiemPolicies.find((p) => p.id === selectedPerDiemPolicyId) ?? effectivePerDiemPolicies[0];
  }, [effectivePerDiemPolicies, selectedPerDiemPolicyId]);

  // Recalculate amount when distance or selected mileage policy changes
  useEffect(() => {
    if (expenseMode === "mileage") {
      if (selectedMileagePolicy && distanceKm) {
        const km = parseFloat(distanceKm);
        const rate = Number(selectedMileagePolicy.rate);
        if (!isNaN(km) && km > 0 && !isNaN(rate) && rate > 0) {
          setAmount((km * rate * (roundTrip ? 2 : 1)).toFixed(2));
          setCurrencyCode(selectedMileagePolicy.currencyCode);
        } else {
          setAmount("");
        }
      } else {
        setAmount("");
      }
    }
  }, [expenseMode, distanceKm, selectedMileagePolicy, roundTrip]);

  // Recalculate amount when days, meals, or selected per-diem policy changes
  useEffect(() => {
    if (expenseMode === "perDiem") {
      if (selectedPerDiemPolicy && daysCount) {
        const days = parseFloat(daysCount);
        const dailyRate = Number(selectedPerDiemPolicy.rate);
        if (!isNaN(days) && days > 0 && !isNaN(dailyRate) && dailyRate > 0) {
          const effBRed = Number(selectedPerDiemPolicy.breakfastReduction ?? 0);
          const effLRed = Number(selectedPerDiemPolicy.lunchReduction ?? 0);
          const effDRed = Number(selectedPerDiemPolicy.dinnerReduction ?? 0);

          const wholeDays = Math.floor(days);
          const partialFactor = days - wholeDays;
          const totalDaysCount = wholeDays + (partialFactor > 0 ? 1 : 0);
          let remainingMeals = providedMeals;
          let computed = 0;

          for (let i = 0; i < totalDaysCount; i++) {
            const factor = (i === totalDaysCount - 1 && partialFactor > 0) ? partialFactor : 1.0;
            const bf = remainingMeals > 0;
            if (bf) remainingMeals--;
            const lu = remainingMeals > 0;
            if (lu) remainingMeals--;
            const di = remainingMeals > 0;
            if (di) remainingMeals--;

            const dayBase = factor * dailyRate;
            const dayRed = (bf ? effBRed : 0) + (lu ? effLRed : 0) + (di ? effDRed : 0);
            computed += Math.max(0, dayBase - dayRed);
          }

          setAmount(computed.toFixed(2));
          setCurrencyCode(selectedPerDiemPolicy.currencyCode);
        } else {
          setAmount("");
        }
      } else {
        setAmount("");
      }
    }
  }, [expenseMode, daysCount, providedMeals, selectedPerDiemPolicy]);

  const categories = useMemo(() => {
    const source = isSelfService ? myCategoriesQuery.data : categoriesQuery.data;
    return (source ?? []).filter((c) => c.status === "Active" && Boolean(c.publishedVersionId));
  }, [categoriesQuery.data, isSelfService, myCategoriesQuery.data]);
  const selectedCategory = categories.find((c) => c.id === categoryId) ?? categories[0];
  const activeVersionId = selectedCategory?.publishedVersionId ?? "";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (expenseMode === "mileage" && !selectedMileagePolicy) {
      setError(t("mileagePerDiem.noMileagePolicyNotice"));
      return;
    }
    if (expenseMode === "perDiem" && !selectedPerDiemPolicy) {
      setError(t("mileagePerDiem.noPerDiemPolicyNotice"));
      return;
    }

    const parsedAmount = parseFloat(amount);
    // Typed travel amounts are a display preview only. The backend recalculates
    // from the selected policy version and overwrites the draft amount.
    const submissionAmount = expenseMode === "general" ? parsedAmount : 1;
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

    let createdExpense: ExpenseDto | null = null;
    try {
      if (isSelfService) {
        createdExpense = await createMyMutation.mutateAsync({
          legalEntityId,
          merchantName: finalMerchant,
          expenseDate,
          amount: submissionAmount,
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
          amount: submissionAmount,
          currencyCode,
          paymentSource,
          categoryVersionId: activeVersionId,
          businessPurpose: finalPurpose,
          description: description.trim() || null
        });
      }

      if (expenseMode === "mileage" && selectedMileagePolicy && distanceKm) {
        const km = parseFloat(distanceKm);
        const setMileage = isSelfService ? expensesApi.setMyMileage : expensesApi.setMileage;
        await setMileage(legalEntityId, createdExpense.id, {
          distance: km,
          rate: 0,
          ratePolicyVersionId: selectedMileagePolicy.id,
          vehicleType: selectedMileagePolicy.vehicleType ?? null,
          route: travelRoute.trim() || null,
          distanceUnit: "KM",
          roundTrip
        });
      } else if (expenseMode === "perDiem" && selectedPerDiemPolicy && daysCount) {
        const days = parseFloat(daysCount);
        const setPerDiem = isSelfService ? expensesApi.setMyPerDiem : expensesApi.setPerDiem;
        await setPerDiem(legalEntityId, createdExpense.id, {
          days,
          dailyRate: 0,
          ratePolicyVersionId: selectedPerDiemPolicy.id,
          destination: destinationCity.trim() || selectedPerDiemPolicy.locationCode || null,
          providedMeals
        });
      }

      onClose();
      setMerchantName("");
      setAmount("");
      setBusinessPurpose("");
      setDescription("");
      setDistanceKm("");
      setTravelRoute("");
      setRoundTrip(false);
      setDaysCount("");
      setDestinationCity("");
      setProvidedMeals(0);
    } catch (err: unknown) {
      // Safe recovery: if expense creation succeeded but typed detail setup failed, delete the draft expense
      if (createdExpense) {
        if (isSelfService) {
          await expensesApi.deleteMyExpense(legalEntityId, createdExpense.id).catch(() => {});
        } else {
          await expensesApi.deleteExpense(legalEntityId, createdExpense.id).catch(() => {});
        }
      }
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
    label: cat.name
  }));

  const mileageVehicleOptions: readonly SelectOption[] = effectiveMileagePolicies.map((p) => ({
    id: p.id,
    label: `${p.vehicleType || t("mileagePerDiem.vehicleType")} (${p.rate} ${p.currencyCode}/كم)`
  }));

  const perDiemLocationOptions: readonly SelectOption[] = effectivePerDiemPolicies.map((p) => ({
    id: p.id,
    label: `${p.locationCode || t("modals.newRatePolicy.locationCodeLabel")} (${p.rate} ${p.currencyCode}/يوم)`
  }));

  const mealReductionOptions: readonly SelectOption[] = [
    { id: "0", label: t("mileagePerDiem.providedMealsNone") },
    { id: "1", label: t("mileagePerDiem.providedMealsOne") },
    { id: "2", label: t("mileagePerDiem.providedMealsTwo") },
    { id: "3", label: t("mileagePerDiem.providedMealsThree") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={isSelfService ? t("selfService.newExpense") : t("modals.newExpense.title")}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            onClick={(e) => void handleSubmit(e)}
            isDisabled={
              isSubmitting ||
              (expenseMode === "mileage" && !selectedMileagePolicy) ||
              (expenseMode === "perDiem" && !selectedPerDiemPolicy)
            }
          >
            {isSubmitting ? t("common.loading") : t("modals.newExpense.submitButton")}
          </Button>
        </div>
      }
    >
      <form onSubmit={(e) => void handleSubmit(e)} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            style={{
              padding: "0.75rem",
              backgroundColor: "#fee2e2",
              color: "#991b1b",
              borderRadius: "0.375rem",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        {/* Expense Kind Switcher */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid #e2e8f0", paddingBottom: "0.5rem" }}>
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
            {effectiveMileagePolicies.length === 0 ? (
              <div style={{ padding: "0.75rem", backgroundColor: "#fff7ed", border: "1px solid #fed7aa", borderRadius: "0.375rem", color: "#9a3412", fontSize: "0.875rem" }}>
                {t("mileagePerDiem.noMileagePolicyNotice")}
              </div>
            ) : (
              <>
                {effectiveMileagePolicies.length > 1 && (
                  <Select
                    label={t("mileagePerDiem.vehicleType")}
                    selectedKey={selectedMileagePolicy?.id ?? effectiveMileagePolicies[0]?.id ?? ""}
                    onSelectionChange={(key) => setSelectedMileagePolicyId(String(key))}
                    options={mileageVehicleOptions}
                  />
                )}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
                  <TextField
                    label={t("mileagePerDiem.distanceKm")}
                    type="number"
                    value={distanceKm}
                    onChange={setDistanceKm}
                    isRequired
                  />
                  <TextField
                    label={t("mileagePerDiem.ratePerKm")}
                    value={selectedMileagePolicy ? `${selectedMileagePolicy.rate} ${selectedMileagePolicy.currencyCode}` : "—"}
                    onChange={() => {}}
                    isReadOnly
                  />
                </div>
                <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "#334155", fontSize: "0.875rem" }}>
                  <input type="checkbox" checked={roundTrip} onChange={(event) => setRoundTrip(event.target.checked)} />
                  {t("mileagePerDiem.roundTrip")}
                </label>
                <TextField
                  label={t("common.description")}
                  placeholder={t("mileagePerDiem.routePlaceholder")}
                  value={travelRoute}
                  onChange={setTravelRoute}
                />
              </>
            )}
          </div>
        )}

        {expenseMode === "perDiem" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", backgroundColor: "#f8fafc", padding: "0.75rem", borderRadius: "0.375rem" }}>
            {effectivePerDiemPolicies.length === 0 ? (
              <div style={{ padding: "0.75rem", backgroundColor: "#fff7ed", border: "1px solid #fed7aa", borderRadius: "0.375rem", color: "#9a3412", fontSize: "0.875rem" }}>
                {t("mileagePerDiem.noPerDiemPolicyNotice")}
              </div>
            ) : (
              <>
                {effectivePerDiemPolicies.length > 1 && (
                  <Select
                    label={t("modals.newRatePolicy.locationCodeLabel")}
                    selectedKey={selectedPerDiemPolicy?.id ?? effectivePerDiemPolicies[0]?.id ?? ""}
                    onSelectionChange={(key) => setSelectedPerDiemPolicyId(String(key))}
                    options={perDiemLocationOptions}
                  />
                )}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
                  <TextField
                    label={t("mileagePerDiem.daysCount")}
                    type="number"
                    value={daysCount}
                    onChange={setDaysCount}
                    isRequired
                  />
                  <TextField
                    label={t("mileagePerDiem.dailyRate")}
                    value={selectedPerDiemPolicy ? `${selectedPerDiemPolicy.rate} ${selectedPerDiemPolicy.currencyCode}` : "—"}
                    onChange={() => {}}
                    isReadOnly
                  />
                </div>
                <Select
                  label={t("mileagePerDiem.providedMeals")}
                  selectedKey={String(providedMeals)}
                  onSelectionChange={(key) => setProvidedMeals(Number(key))}
                  options={mealReductionOptions}
                />
                <TextField
                  label={t("modals.newRatePolicy.locationCodeLabel")}
                  placeholder={t("mileagePerDiem.locationPlaceholder")}
                  value={destinationCity}
                  onChange={setDestinationCity}
                />
              </>
            )}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.newExpense.amountLabel")}
            type="number"
            value={amount}
            onChange={setAmount}
            isReadOnly={expenseMode === "mileage" || expenseMode === "perDiem"}
            isRequired
          />

          <TextField
            label={t("modals.newExpense.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val.toUpperCase())}
            isReadOnly={expenseMode === "mileage" || expenseMode === "perDiem"}
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
