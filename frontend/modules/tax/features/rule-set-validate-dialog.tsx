import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { taxApi } from "../services/tax-api.ts";
import { extractTaxError } from "../services/tax-error-map.ts";

export interface RuleSetValidateDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly ruleSetId: string;
  readonly legalEntityId: string;
}

export function RuleSetValidateDialog({
  isOpen,
  onClose,
  ruleSetId,
  legalEntityId
}: RuleSetValidateDialogProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [isLoading, setIsLoading] = useState(false);
  const [isValidated, setIsValidated] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [technicalCode, setTechnicalCode] = useState<string | null>(null);

  const handleRunValidation = async () => {
    setIsLoading(true);
    setValidationError(null);
    setTechnicalCode(null);
    try {
      await taxApi.validateRuleSet(ruleSetId, legalEntityId);
      setIsValidated(true);
    } catch (err) {
      const mapped = extractTaxError(err);
      setValidationError(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
      setTechnicalCode(mapped.code);
      setIsValidated(false);
    } finally {
      setIsLoading(false);
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.close")}
      </Button>
      {!isValidated && (
        <Button variant="primary" onPress={() => void handleRunValidation()} isLoading={isLoading}>
          {t("rules.runValidationAction")}
        </Button>
      )}
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("rules.validateTitle")}
      description={t("rules.validateDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {!isValidated && !validationError && !isLoading && (
          <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
            {t("rules.validatePrompt")}
          </p>
        )}

        {isLoading && (
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "0.5rem", padding: "1.5rem" }}>
            <Spinner size="large" />
            <span style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("rules.validating")}
            </span>
          </div>
        )}

        {isValidated && (
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-success-subtle, #f0fdf4)",
              color: "var(--color-status-success-text, #166534)",
              fontSize: "0.875rem",
              display: "flex",
              flexDirection: "column",
              gap: "0.25rem"
            }}
          >
            <strong>✓ {t("rules.validationSuccessTitle")}</strong>
            <span>{t("rules.validationSuccessDescription")}</span>
          </div>
        )}

        {validationError && (
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem",
              display: "flex",
              flexDirection: "column",
              gap: "0.5rem"
            }}
          >
            <strong>✕ {t("rules.validationErrorTitle")}</strong>
            <span>{validationError}</span>
            {technicalCode && (
              <span style={{ fontSize: "0.75rem", fontFamily: "var(--font-mono, monospace)", color: "var(--color-content-muted)" }}>
                {t("rules.errorCodeLabel")}: {technicalCode}
              </span>
            )}
          </div>
        )}
      </div>
    </Dialog>
  );
}
