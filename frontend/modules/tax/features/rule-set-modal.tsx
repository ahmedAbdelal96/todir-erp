import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { useCreateRuleSetMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";

export interface RuleSetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly defaultCountryCode?: string | undefined;
  readonly defaultEffectiveDate: string;
}

export function RuleSetModal({
  isOpen,
  onClose,
  legalEntityId,
  defaultCountryCode = "EG",
  defaultEffectiveDate
}: RuleSetModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [countryCode, setCountryCode] = useState(defaultCountryCode);
  const [schemeCode, setSchemeCode] = useState("DEVELOPMENT");
  const [version, setVersion] = useState("1");
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [effectiveTo, setEffectiveTo] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useCreateRuleSetMutation();

  const handleSave = async () => {
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        legalEntityId: legalEntityId || null,
        countryCode: countryCode.toUpperCase(),
        schemeCode: schemeCode || null,
        version: parseInt(version, 10) || 1,
        sourceKind: "TenantConfiguration",
        mutable: true,
        effectiveFrom,
        effectiveTo: effectiveTo || null
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
      title={t("rules.createSetTitle")}
      description={t("rules.createSetDescription")}
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

        <TextField
          label={t("rules.countryCode")}
          value={countryCode}
          onChange={setCountryCode}
          isRequired
        />

        <TextField
          label={t("rules.schemeCode")}
          value={schemeCode}
          onChange={setSchemeCode}
        />

        <TextField
          label={t("rules.version")}
          type="number"
          value={version}
          onChange={setVersion}
          isRequired
        />

        <LocalDateField
          label={t("rules.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        <LocalDateField
          label={t("rules.effectiveTo")}
          value={effectiveTo}
          onChange={setEffectiveTo}
        />

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("rules.draftSetNotice")}
        </p>
      </div>
    </Dialog>
  );
}
