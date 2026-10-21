import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import {
  useCreateExchangeRateMutation,
  useFinancialPolicy
} from "../hooks/finbase-queries.ts";
import type { ExchangeRatePurpose } from "../types.ts";

export interface CreateRateModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly entityId: string;
  readonly baseCurrencyCode: string;
}

const PURPOSES: readonly ExchangeRatePurpose[] = [
  "Accounting",
  "Commercial",
  "Contractual",
  "BankActual",
  "Settlement",
  "Tax"
];

export function CreateRateModal({
  isOpen,
  onClose,
  entityId,
  baseCurrencyCode
}: CreateRateModalProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const createRateMutation = useCreateExchangeRateMutation();
  const { data: policy } = useFinancialPolicy(entityId);

  const [fromCurrency, setFromCurrency] = useState("USD");
  const [toCurrency, setToCurrency] = useState(baseCurrencyCode);
  const [rate, setRate] = useState("");
  const [effectiveDate, setEffectiveDate] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [purpose, setPurpose] = useState<ExchangeRatePurpose>("Accounting");
  const [providerReference, setProviderReference] = useState("");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string>();

  const isOverrideAllowed = policy?.manualRateOverrideAllowed ?? false;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(undefined);

    const normalizedRate = normalizeDecimalInput(rate);
    if (!normalizedRate || normalizedRate === "0") {
      setErrorMessage(t("rates.createModal.rateRequiredError"));
      return;
    }
    if (!reason.trim()) {
      setErrorMessage(t("rates.createModal.reasonRequiredError"));
      return;
    }

    try {
      await createRateMutation.mutateAsync({
        legalEntityId: entityId,
        fromCurrency: fromCurrency.trim().toUpperCase(),
        toCurrency: toCurrency.trim().toUpperCase(),
        rate: normalizedRate,
        purpose,
        effectiveDate,
        sourceType: "Manual",
        providerReference: providerReference.trim() || null,
        reason: reason.trim()
      });
      onClose();
    } catch (err: unknown) {
      const errObj = err as { problemDetails?: { code?: string; extensions?: { code?: string } }; message?: string };
      const code = errObj?.problemDetails?.code || errObj?.problemDetails?.extensions?.code || "";
      if (code === "finbase.rate_override_not_allowed" || errObj?.message?.includes("rate_override_not_allowed") || String(err).includes("rate_override_not_allowed")) {
        setErrorMessage(t("rates.createModal.overrideDisallowedError"));
      } else {
        setErrorMessage(t("rates.createModal.submitError"));
      }
    }
  };

  const purposeOptions = PURPOSES.map((p) => ({
    id: p,
    label: t(`rates.purposes.${p}`)
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("rates.createModal.title")}
      description={t("rates.createModal.description")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {!isOverrideAllowed ? (
          <Alert variant="warning" title={t("rates.createModal.policyNoticeTitle")}>
            {t("rates.createModal.policyNoticeDisabled")}
          </Alert>
        ) : null}

        {errorMessage ? (
          <Alert variant="danger" title={t("rates.createModal.errorTitle")}>
            {errorMessage}
          </Alert>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
          <TextField
            label={t("rates.createModal.fromCurrencyLabel")}
            value={fromCurrency}
            onChange={(val) => setFromCurrency(val.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
          <TextField
            label={t("rates.createModal.toCurrencyLabel")}
            value={toCurrency}
            onChange={(val) => setToCurrency(val.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
        </div>

        <div style={{ padding: "var(--space-3)", backgroundColor: "var(--color-surface-subtle)", borderRadius: "var(--radius-md)", fontSize: "var(--font-size-xs)", border: "1px solid var(--color-border-subtle)" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-2)", gap: "var(--space-2)" }}>
            <span style={{ fontWeight: 600, color: "var(--color-content-secondary)" }}>
              {t("rates.createModal.directionHint")}
            </span>
            <span style={{ color: "var(--color-content-muted)", fontSize: "11px" }}>
              {t("rates.createModal.directionExplanation")}
            </span>
          </div>
          <div
            dir="ltr"
            style={{
              display: "flex",
              flexDirection: "row",
              alignItems: "center",
              justifyContent: "center",
              direction: "ltr",
              padding: "var(--space-2)",
              backgroundColor: "var(--color-surface-raised)",
              borderRadius: "var(--radius-sm)"
            }}
          >
            <span style={{ fontWeight: 700, fontSize: "var(--font-size-sm)", color: "var(--color-primary-emphasis)", letterSpacing: "0.02em" }}>
              {t("rates.createModal.rateEquation", {
                from: fromCurrency || baseCurrencyCode || t("rates.createModal.fromFallback"),
                rate: rate ? `[${rate}]` : `[${t("rates.createModal.rateFallback")}]`,
                to: toCurrency || t("rates.createModal.toFallback")
              })}
            </span>
          </div>
          <div style={{ marginTop: "var(--space-2)", textAlign: "center", color: "var(--color-content-muted)", fontSize: "11px" }}>
            {t("rates.createModal.formulaHelper", { base: fromCurrency || "USD", quote: toCurrency || "EGP" })}
          </div>
        </div>

        <TextField
          label={t("rates.createModal.rateLabel")}
          description={t("rates.createModal.rateDescription")}
          value={rate}
          onChange={setRate}
          inputDir="ltr"
          placeholder={t("rates.createModal.ratePlaceholder")}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
          <TextField
            label={t("rates.createModal.effectiveDateLabel")}
            value={effectiveDate}
            onChange={setEffectiveDate}
            inputDir="ltr"
            placeholder={t("rates.createModal.datePlaceholder")}
            isRequired
          />
          <Select
            label={t("rates.createModal.purposeLabel")}
            selectedKey={purpose}
            onSelectionChange={(key) => setPurpose(key as ExchangeRatePurpose)}
            options={purposeOptions}
          />
        </div>

        <TextField
          label={t("rates.createModal.providerReferenceLabel")}
          description={t("rates.createModal.providerReferenceDescription")}
          value={providerReference}
          onChange={setProviderReference}
          placeholder={t("rates.createModal.referencePlaceholder")}
        />

        <TextField
          label={t("rates.createModal.reasonLabel")}
          description={t("rates.createModal.reasonDescription")}
          value={reason}
          onChange={setReason}
          placeholder={t("rates.createModal.reasonPlaceholder")}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-4)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("rates.createModal.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createRateMutation.isPending || !rate || !reason.trim()}
          >
            {createRateMutation.isPending
              ? t("rates.createModal.saving")
              : t("rates.createModal.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
