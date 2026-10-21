import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useEnableCurrencyMutation, useFinBaseCurrencies, useEntityCurrencies } from "../hooks/finbase-queries.ts";

export interface EnableCurrencyModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly entityId: string;
}

export function EnableCurrencyModal({
  isOpen,
  onClose,
  entityId
}: EnableCurrencyModalProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const { data: referenceCurrencies = [] } = useFinBaseCurrencies();
  const { data: entityCurrencies = [] } = useEntityCurrencies(entityId);
  const enableMutation = useEnableCurrencyMutation();

  const enabledCodes = new Set(entityCurrencies.map((c) => c.currencyCode.toUpperCase()));
  const availableCurrencies = referenceCurrencies.filter(
    (c) => !enabledCodes.has(c.code.toUpperCase())
  );

  const [selectedCode, setSelectedCode] = useState<string>(
    availableCurrencies[0]?.code ?? ""
  );
  const [allowSales, setAllowSales] = useState(true);
  const [allowPurchase, setAllowPurchase] = useState(true);
  const [allowPayment, setAllowPayment] = useState(true);
  const [allowManualRate, setAllowManualRate] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string>();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCode) return;
    setErrorMessage(undefined);

    try {
      await enableMutation.mutateAsync({
        entityId,
        code: selectedCode,
        request: {
          allowSales,
          allowPurchase,
          allowPayment,
          allowManualRate
        }
      });
      onClose();
    } catch {
      setErrorMessage(t("currencies.enableModal.submitError"));
    }
  };

  const currencyOptions = availableCurrencies.map((c) => ({
    id: c.code,
    label: `${c.code} — ${c.nameAr} (${c.nameEn})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("currencies.enableModal.title")}
      description={t("currencies.enableModal.description")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {errorMessage ? (
          <Alert variant="danger" title={t("currencies.enableModal.errorTitle")}>
            {errorMessage}
          </Alert>
        ) : null}

        {availableCurrencies.length === 0 ? (
          <Alert variant="info" title={t("currencies.enableModal.allEnabledTitle")}>
            {t("currencies.enableModal.allEnabledDescription")}
          </Alert>
        ) : (
          <>
            <Select
              label={t("currencies.enableModal.currencyLabel")}
              selectedKey={selectedCode || availableCurrencies[0]?.code}
              onSelectionChange={(key) => setSelectedCode(String(key))}
              options={currencyOptions}
            />

            <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)", marginTop: "var(--space-2)" }}>
              <span style={{ fontSize: "var(--font-size-sm)", fontWeight: 600 }}>
                {t("currencies.enableModal.permissionsTitle")}
              </span>
              <Checkbox isSelected={allowSales} onChange={setAllowSales}>
                {t("currencies.enableModal.allowSales")}
              </Checkbox>
              <Checkbox isSelected={allowPurchase} onChange={setAllowPurchase}>
                {t("currencies.enableModal.allowPurchase")}
              </Checkbox>
              <Checkbox isSelected={allowPayment} onChange={setAllowPayment}>
                {t("currencies.enableModal.allowPayment")}
              </Checkbox>
              <Checkbox isSelected={allowManualRate} onChange={setAllowManualRate}>
                {t("currencies.enableModal.allowManualRate")}
              </Checkbox>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", marginTop: "var(--space-4)" }}>
              <Button variant="secondary" type="button" onPress={onClose}>
                {t("currencies.enableModal.cancel")}
              </Button>
              <Button
                variant="primary"
                type="submit"
                isDisabled={!selectedCode || enableMutation.isPending}
              >
                {enableMutation.isPending
                  ? t("currencies.enableModal.saving")
                  : t("currencies.enableModal.submit")}
              </Button>
            </div>
          </>
        )}
      </form>
    </Dialog>
  );
}
