import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useCreateRateSourceMutation } from "../hooks/finbase-queries.ts";
import type { ExchangeRatePurpose } from "../types.ts";

export interface RateSourceModalProps {
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

export function RateSourceModal({
  isOpen,
  onClose,
  entityId,
  baseCurrencyCode
}: RateSourceModalProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const createMutation = useCreateRateSourceMutation();

  const [sourceCode, setSourceCode] = useState("CBE");
  const [fromCurrency, setFromCurrency] = useState("USD");
  const [toCurrency, setToCurrency] = useState(baseCurrencyCode);
  const [purpose, setPurpose] = useState<ExchangeRatePurpose>("Accounting");
  const [priority, setPriority] = useState(1);
  const [syncInterval, setSyncInterval] = useState(3600);
  const [credentialReference, setCredentialReference] = useState("");
  const [errorMessage, setErrorMessage] = useState<string>();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(undefined);

    if (!sourceCode.trim()) {
      setErrorMessage(t("rateSources.modal.sourceRequiredError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        entityId,
        request: {
          sourceCode: sourceCode.trim().toUpperCase(),
          fromCurrency: fromCurrency.trim().toUpperCase(),
          toCurrency: toCurrency.trim().toUpperCase(),
          purpose,
          priority,
          credentialReference: credentialReference.trim() || null,
          syncIntervalSeconds: syncInterval
        }
      });
      onClose();
    } catch {
      setErrorMessage(t("rateSources.modal.submitError"));
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
      title={t("rateSources.modal.title")}
      description={t("rateSources.modal.description")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {errorMessage ? (
          <Alert variant="danger" title={t("rateSources.modal.errorTitle")}>
            {errorMessage}
          </Alert>
        ) : null}

        <TextField
          label={t("rateSources.modal.sourceCodeLabel")}
          description={t("rateSources.modal.sourceCodeDescription")}
          value={sourceCode}
          onChange={(v) => setSourceCode(v.toUpperCase())}
          inputDir="ltr"
          placeholder={t("rateSources.modal.sourceCodePlaceholder")}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
          <TextField
            label={t("rateSources.modal.fromCurrencyLabel")}
            value={fromCurrency}
            onChange={(v) => setFromCurrency(v.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
          <TextField
            label={t("rateSources.modal.toCurrencyLabel")}
            value={toCurrency}
            onChange={(v) => setToCurrency(v.toUpperCase())}
            inputDir="ltr"
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" }}>
          <Select
            label={t("rateSources.modal.purposeLabel")}
            selectedKey={purpose}
            onSelectionChange={(k) => setPurpose(k as ExchangeRatePurpose)}
            options={purposeOptions}
          />
          <TextField
            label={t("rateSources.modal.priorityLabel")}
            description={t("rateSources.modal.priorityDescription")}
            value={String(priority)}
            onChange={(v) => setPriority(parseInt(v, 10) || 1)}
            inputDir="ltr"
          />
        </div>

        <TextField
          label={t("rateSources.modal.syncIntervalLabel")}
          description={t("rateSources.modal.syncIntervalDescription")}
          value={String(syncInterval)}
          onChange={(v) => setSyncInterval(parseInt(v, 10) || 3600)}
          inputDir="ltr"
        />

        <TextField
          label={t("rateSources.modal.credentialReferenceLabel")}
          description={t("rateSources.modal.credentialReferenceDescription")}
          value={credentialReference}
          onChange={setCredentialReference}
          inputDir="ltr"
          placeholder={t("rateSources.modal.credentialPlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-4)" }}>
          <Button variant="secondary" type="button" onPress={onClose}>
            {t("rateSources.modal.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createMutation.isPending || !sourceCode}
          >
            {createMutation.isPending
              ? t("rateSources.modal.saving")
              : t("rateSources.modal.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
