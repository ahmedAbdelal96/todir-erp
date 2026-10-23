import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { useSetTaxCapabilityMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxCapability } from "../types.ts";

export interface TaxCapabilityModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly currentCapability: TaxCapability;
  readonly defaultEffectiveDate: string;
}

export function TaxCapabilityModal({
  isOpen,
  onClose,
  legalEntityId,
  currentCapability,
  defaultEffectiveDate
}: TaxCapabilityModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");
  const [capability, setCapability] = useState<TaxCapability>(
    currentCapability === "Enabled" ? "Disabled" : "Enabled"
  );
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useSetTaxCapabilityMutation();

  const handleSave = async () => {
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        legalEntityId,
        request: {
          capability,
          effectiveFrom
        }
      });
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button variant="primary" onPress={handleSave} isLoading={mutation.isPending}>
        {t("actions.save")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("capability.modalTitle")}
      description={t("capability.modalDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <Select
          label={t("capability.fieldLabel")}
          selectedKey={capability}
          onSelectionChange={(key) => setCapability(key as TaxCapability)}
          options={[
            { id: "Enabled", label: t("capability.enabled") },
            { id: "Disabled", label: t("capability.disabled") }
          ]}
        />

        <LocalDateField
          label={t("capability.effectiveFrom")}
          value={effectiveFrom}
          onChange={(val) => setEffectiveFrom(val)}
          isRequired
        />

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {capability === "Enabled"
            ? t("capability.enableGuidance")
            : t("capability.disableGuidance")}
        </p>
      </div>
    </Dialog>
  );
}
