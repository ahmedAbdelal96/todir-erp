import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { useTransitionRuleSetMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxRuleSetDto } from "../types.ts";

export interface RuleSetActivateDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly ruleSet: TaxRuleSetDto;
  readonly legalEntityId: string;
}

export function RuleSetActivateDialog({
  isOpen,
  onClose,
  ruleSet,
  legalEntityId
}: RuleSetActivateDialogProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useTransitionRuleSetMutation();

  const handleActivate = async () => {
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        ruleSetId: ruleSet.id,
        legalEntityId,
        transition: "activate"
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
      <Button variant="primary" onPress={() => void handleActivate()} isLoading={mutation.isPending}>
        {t("rules.confirmActivateAction")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={t("rules.activateConfirmTitle")}
      description={t("rules.activateConfirmDescription")}
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

        <div
          style={{
            padding: "1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)",
            fontSize: "0.875rem",
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "0.75rem"
          }}
        >
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("rules.country")}</span>
            <strong>{ruleSet.countryCode}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("rules.version")}</span>
            <strong>{t("registrations.versionTag", { version: ruleSet.version })}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("rules.effectiveFrom")}</span>
            <strong>{ruleSet.effectiveFrom}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("rules.status")}</span>
            <strong>{ruleSet.status}</strong>
          </div>
        </div>

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-status-warning-text, #92400e)" }}>
          {t("rules.activatePrivilegedNotice")}
        </p>
      </div>
    </Dialog>
  );
}
