import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useCreatePaymentTermMutation } from "../hooks/finbase-queries.ts";
import type { DueDateBasis, PaymentAllocation, PaymentTermRuleRequest } from "../types.ts";

export interface CreatePaymentTermModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

const ALLOCATIONS: readonly PaymentAllocation[] = ["Percentage", "Fixed", "Balance"];
const DUE_BASES: readonly DueDateBasis[] = [
  "Immediate",
  "DaysAfterDate",
  "EndOfMonth",
  "DaysAfterEndOfMonth",
  "DayOfFollowingMonth"
];

export function CreatePaymentTermModal({
  isOpen,
  onClose
}: CreatePaymentTermModalProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const createMutation = useCreatePaymentTermMutation();

  const [code, setCode] = useState("");
  const [displayNameAr, setDisplayNameAr] = useState("");
  const [displayNameEn, setDisplayNameEn] = useState("");
  const [description, setDescription] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );

  const [rules, setRules] = useState<readonly PaymentTermRuleRequest[]>([
    {
      sequence: 1,
      allocationType: "Balance",
      dueDateBasis: "Immediate",
      days: 0,
      monthOffset: 0
    }
  ]);
  const [errorMessage, setErrorMessage] = useState<string>();

  const handleAddRule = () => {
    setRules((prev) => [
      ...prev,
      {
        sequence: prev.length + 1,
        allocationType: "Balance",
        dueDateBasis: "DaysAfterDate",
        days: 30,
        monthOffset: 0
      }
    ]);
  };

  const handleRemoveRule = (index: number) => {
    if (rules.length <= 1) return;
    setRules((prev) =>
      prev
        .filter((_, i) => i !== index)
        .map((r, i) => ({ ...r, sequence: i + 1 }))
    );
  };

  const handleUpdateRule = (index: number, update: Partial<PaymentTermRuleRequest>) => {
    setRules((prev) => {
      const current = prev[index];
      if (!current) return prev;
      const next = [...prev];
      const merged: PaymentTermRuleRequest = {
        sequence: update.sequence ?? current.sequence,
        allocationType: update.allocationType ?? current.allocationType,
        percentage: update.percentage !== undefined ? update.percentage : current.percentage,
        fixedAmount: update.fixedAmount !== undefined ? update.fixedAmount : current.fixedAmount,
        dueDateBasis: update.dueDateBasis ?? current.dueDateBasis,
        days: update.days ?? current.days,
        dayOfMonth: update.dayOfMonth !== undefined ? update.dayOfMonth : current.dayOfMonth,
        monthOffset: update.monthOffset ?? current.monthOffset
      };
      next[index] = merged;
      return next;
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(undefined);

    if (!code.trim() || !displayNameAr.trim() || !displayNameEn.trim()) {
      setErrorMessage(t("paymentTerms.createModal.requiredFieldsError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        code: code.trim().toUpperCase(),
        displayNameAr: displayNameAr.trim(),
        displayNameEn: displayNameEn.trim(),
        description: description.trim() || null,
        effectiveFrom,
        rules
      });
      onClose();
    } catch {
      setErrorMessage(t("paymentTerms.createModal.submitError"));
    }
  };

  const allocationOptions = ALLOCATIONS.map((a) => ({
    id: a,
    label: t(`paymentTerms.allocations.${a}`)
  }));

  const dueBasisOptions = DUE_BASES.map((b) => ({
    id: b,
    label: t(`paymentTerms.dueBases.${b}`)
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("paymentTerms.createModal.title")}
      description={t("paymentTerms.createModal.description")}
      size="large"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {errorMessage ? (
          <Alert variant="danger" title={t("paymentTerms.createModal.errorTitle")}>
            {errorMessage}
          </Alert>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "var(--space-3)" }}>
          <TextField
            label={t("paymentTerms.createModal.codeLabel")}
            value={code}
            onChange={(v) => setCode(v.toUpperCase())}
            inputDir="ltr"
            placeholder={t("paymentTerms.createModal.codePlaceholder")}
            isRequired
          />
          <TextField
            label={t("paymentTerms.createModal.nameArLabel")}
            value={displayNameAr}
            onChange={setDisplayNameAr}
            placeholder={t("paymentTerms.createModal.nameArPlaceholder")}
            isRequired
          />
          <TextField
            label={t("paymentTerms.createModal.nameEnLabel")}
            value={displayNameEn}
            onChange={setDisplayNameEn}
            inputDir="ltr"
            placeholder={t("paymentTerms.createModal.nameEnPlaceholder")}
            isRequired
          />
          <TextField
            label={t("paymentTerms.createModal.effectiveFromLabel")}
            value={effectiveFrom}
            onChange={setEffectiveFrom}
            inputDir="ltr"
            isRequired
          />
        </div>

        <TextField
          label={t("paymentTerms.createModal.descriptionLabel")}
          value={description}
          onChange={setDescription}
          placeholder={t("paymentTerms.createModal.descriptionPlaceholder")}
        />

        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)", marginTop: "var(--space-2)" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontSize: "var(--font-size-sm)", fontWeight: 700 }}>
              {t("paymentTerms.createModal.rulesTitle")}
            </span>
            <Button variant="secondary" type="button" onPress={handleAddRule}>
              {t("paymentTerms.createModal.addRuleButton")}
            </Button>
          </div>

          {rules.map((rule, idx) => (
            <div
              key={idx}
              style={{
                border: "1px solid var(--color-border-subtle)",
                borderRadius: "var(--radius-md)",
                padding: "var(--space-3)",
                display: "grid",
                gridTemplateColumns: "60px 140px 120px 160px 90px 40px",
                gap: "var(--space-2)",
                alignItems: "center"
              }}
            >
              <div style={{ fontWeight: 600, fontSize: "var(--font-size-sm)", textAlign: "center" }}>
                #{rule.sequence}
              </div>

              <Select
                label={t("paymentTerms.rules.allocation")}
                selectedKey={rule.allocationType}
                onSelectionChange={(k) =>
                  handleUpdateRule(idx, {
                    allocationType: k as PaymentAllocation,
                    percentage: k === "Percentage" ? "0.5" : null,
                    fixedAmount: k === "Fixed" ? "100.00" : null
                  })
                }
                options={allocationOptions}
              />

              {rule.allocationType === "Percentage" ? (
                <TextField
                  label={t("paymentTerms.rules.percentageRatio")}
                  value={String(rule.percentage ?? "")}
                  onChange={(v) => handleUpdateRule(idx, { percentage: v })}
                  inputDir="ltr"
                  placeholder={t("paymentTerms.createModal.ratioPlaceholder")}
                />
              ) : rule.allocationType === "Fixed" ? (
                <TextField
                  label={t("paymentTerms.rules.fixedAmount")}
                  value={rule.fixedAmount ?? ""}
                  onChange={(v) => handleUpdateRule(idx, { fixedAmount: v })}
                  inputDir="ltr"
                  placeholder={t("paymentTerms.createModal.fixedAmountPlaceholder")}
                />
              ) : (
                <div style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)", alignSelf: "center" }}>
                  {t("paymentTerms.rules.balanceAuto")}
                </div>
              )}

              <Select
                label={t("paymentTerms.rules.dueBasis")}
                selectedKey={rule.dueDateBasis}
                onSelectionChange={(k) =>
                  handleUpdateRule(idx, { dueDateBasis: k as DueDateBasis })
                }
                options={dueBasisOptions}
              />

              <TextField
                label={t("paymentTerms.rules.days")}
                value={String(rule.days)}
                onChange={(v) => handleUpdateRule(idx, { days: parseInt(v, 10) || 0 })}
                inputDir="ltr"
              />

              {rules.length > 1 ? (
                <div style={{ alignSelf: "center", justifySelf: "center" }}>
                  <Button
                    variant="danger"
                    type="button"
                    onPress={() => handleRemoveRule(idx)}
                  >
                    ×
                  </Button>
                </div>
              ) : <div />}
            </div>
          ))}
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-4)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("paymentTerms.createModal.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createMutation.isPending || !code || !displayNameAr || !displayNameEn}
          >
            {createMutation.isPending
              ? t("paymentTerms.createModal.saving")
              : t("paymentTerms.createModal.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
